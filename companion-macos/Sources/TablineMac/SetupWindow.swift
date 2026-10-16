import AppKit
import ApplicationServices
import TablineCore

@MainActor final class SetupWindow:NSObject,NSWindowDelegate {
    let window=NSWindow(contentRect:CGRect(x:0,y:0,width:590,height:540),styleMask:[.titled,.closable,.miniaturizable],backing:.buffered,defer:false)
    private let idField=NSTextField(string:"")
    private let status=NSTextField(wrappingLabelWithString:"")
    private let permissions=NSTextField(wrappingLabelWithString:"")
    private var timer:Timer?
    private var pendingID:String?
    private lazy var installButton=button("Install Tabline",#selector(install))
    private lazy var connectButton=button("Connect & allow screen access",#selector(connect))
    private let intro=NSTextField(wrappingLabelWithString:"")
    private lazy var allowButton=button("Allow Screen Recording…",#selector(screenPermission))
    private lazy var restoreButton=button("Optional: allow window restoration…",#selector(accessibility))
    var standalone=false
    var register:( (String)throws->Void )?
    override init(){
        super.init();window.title="Tabline Companion · Mac setup";window.isReleasedWhenClosed=false;window.delegate=self
        let stack=NSStackView();stack.orientation = .vertical;stack.alignment = .leading;stack.spacing=15
        stack.edgeInsets=NSEdgeInsets(top:28,left:28,bottom:28,right:28);stack.translatesAutoresizingMaskIntoConstraints=false
        let title=NSTextField(labelWithString:"Screenshots on a string.");title.font = .systemFont(ofSize:29,weight:.semibold)
        intro.font = .systemFont(ofSize:14)
        idField.placeholderString="Paste the 32-letter Chrome extension ID";idField.font = .monospacedSystemFont(ofSize:13,weight:.regular)
        stack.addArrangedSubview(title);stack.addArrangedSubview(intro);stack.addArrangedSubview(idField)
        stack.addArrangedSubview(installButton)
        stack.addArrangedSubview(connectButton)
        status.textColor = .secondaryLabelColor;stack.addArrangedSubview(status)
        stack.addArrangedSubview(allowButton)
        stack.addArrangedSubview(restoreButton)
        permissions.font = .systemFont(ofSize:13);permissions.textColor = .secondaryLabelColor;stack.addArrangedSubview(permissions)
        let note=NSTextField(wrappingLabelWithString:"Screen access lets Tabline capture desktop windows. After approving access, return to Chrome; the setup page will detect the connection automatically. Restart Chrome only if macOS requests it. Window restoration is optional.")
        note.font = .systemFont(ofSize:12);note.textColor = .secondaryLabelColor;stack.addArrangedSubview(note)
        stack.addArrangedSubview(button("Quit companion",#selector(quit)))
        window.contentView?.addSubview(stack)
        NSLayoutConstraint.activate([stack.leadingAnchor.constraint(equalTo:window.contentView!.leadingAnchor),stack.trailingAnchor.constraint(equalTo:window.contentView!.trailingAnchor),stack.topAnchor.constraint(equalTo:window.contentView!.topAnchor),idField.widthAnchor.constraint(equalToConstant:530)])
    }
    private func button(_ title:String,_ action:Selector)->NSButton{let value=NSButton(title:title,target:self,action:action);value.bezelStyle = .rounded;return value}
    func show(){
        let installed=MacInstaller.installed
        installButton.isHidden=installed;connectButton.isHidden = !installed || pendingID==nil
        idField.isHidden=pendingID==nil;idField.isEditable=false
        allowButton.isHidden = !installed;restoreButton.isHidden = !installed
        intro.stringValue = !installed ? "Choose Install Tabline to add this app to your Applications folder. No terminal commands or processor selection needed." : (pendingID==nil ? "Return to Tabline’s Chrome setup page and choose Connect Tabline. This will link your extension automatically." : "Your Chrome extension is requesting access. Choose Connect & allow screen access to approve this connection and finish setup.")
        window.center();NSApp.activate(ignoringOtherApps:true);window.makeKeyAndOrderFront(nil);refresh();timer?.invalidate();timer=Timer.scheduledTimer(withTimeInterval:1,repeats:true){[weak self] _ in MainActor.assumeIsolated { self?.refresh() }}
    }
    func requestConnection(_ id:String){guard NativeFraming.extensionID(id) else{message("Invalid Chrome extension connection request.");return};pendingID=id;idField.stringValue=id;show()}
    @objc private func install(){do{try MacInstaller.install(extensionID:pendingID){[weak self] error in self?.message(error ?? "Installed. Return to Chrome and choose Connect Tabline.")}}catch{message(error.localizedDescription)}}
    func message(_ text:String){status.stringValue=text}
    private func refresh(){permissions.stringValue="Screen access: \(CGPreflightScreenCaptureAccess() ? "allowed":"not allowed")    ·    Window restoration: \(AXIsProcessTrusted() ? "allowed":"optional / off")"}
    @objc private func connect(){guard MacInstaller.installed,let id=pendingID,NativeFraming.extensionID(id) else{message("Open the connection from Tabline’s Chrome setup page.");return};do{try register?(id);message("Connected. Approve screen access, then return to Chrome.");screenPermission()}catch{message(error.localizedDescription)}}
    @objc private func screenPermission(){if !CGPreflightScreenCaptureAccess(){_ = CGRequestScreenCaptureAccess()};if !CGPreflightScreenCaptureAccess(){NSWorkspace.shared.open(URL(string:"x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture")!)};refresh()}
    @objc private func accessibility(){let key=kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String;_ = AXIsProcessTrustedWithOptions([key:true] as CFDictionary);refresh()}
    @objc private func quit(){NSApp.terminate(nil)}
    func windowWillClose(_ notification:Notification){timer?.invalidate();timer=nil;if standalone{NSApp.terminate(nil)}}
    static func register(_ extensionID:String)throws{
        guard NativeFraming.extensionID(extensionID),let executable=Bundle.main.executableURL,Bundle.main.bundleIdentifier=="com.tabline.companion" else{throw CompanionError.message("Open the packaged Tabline Companion app, not the raw development executable.")}
        guard MacInstaller.installed else{throw CompanionError.message("Install Tabline first so Chrome always connects to the same app location.")}
        // Chrome for Testing uses a separate host directory starting in Chrome 146.
        for browser in ["Google/Chrome","Google/ChromeForTesting","Chromium"] {
            let folder=FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/\(browser)/NativeMessagingHosts",isDirectory:true)
            try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
            let target=folder.appendingPathComponent("com.tabline.companion.json")
            var origins=["chrome-extension://\(extensionID)/"]
            if let data=try? Data(contentsOf:target),let old=(try? JSONSerialization.jsonObject(with:data)) as? [String:Any],old["path"] as? String==executable.path,let allowed=old["allowed_origins"] as? [String]{origins += allowed.filter{origin in let id=origin.replacingOccurrences(of:"chrome-extension://",with:"").trimmingCharacters(in:CharacterSet(charactersIn:"/"));return NativeFraming.extensionID(id)}}
            let manifest:[String:Any]=["name":"com.tabline.companion","description":"Local Tabline Mac window previews and screenshot clipboard","path":executable.path,"type":"stdio","allowed_origins":Array(Set(origins)).sorted()]
            try JSONSerialization.data(withJSONObject:manifest,options:[.prettyPrinted,.sortedKeys]).write(to:target,options:.atomic)
        }
    }
}
