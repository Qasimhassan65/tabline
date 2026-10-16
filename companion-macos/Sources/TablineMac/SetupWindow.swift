import AppKit
import ApplicationServices
import TablineCore

@MainActor final class SetupWindow:NSObject,NSWindowDelegate {
    let window=NSWindow(contentRect:CGRect(x:0,y:0,width:590,height:540),styleMask:[.titled,.closable,.miniaturizable],backing:.buffered,defer:false)
    private let idField=NSTextField(string:"")
    private let status=NSTextField(wrappingLabelWithString:"")
    private let permissions=NSTextField(wrappingLabelWithString:"")
    private var timer:Timer?
    var standalone=false
    var register:( (String)throws->Void )?
    override init(){
        super.init();window.title="Tabline Companion · Mac setup";window.isReleasedWhenClosed=false;window.delegate=self
        let stack=NSStackView();stack.orientation = .vertical;stack.alignment = .leading;stack.spacing=15
        stack.edgeInsets=NSEdgeInsets(top:28,left:28,bottom:28,right:28);stack.translatesAutoresizingMaskIntoConstraints=false
        let title=NSTextField(labelWithString:"Screenshots on a string.");title.font = .systemFont(ofSize:29,weight:.semibold)
        let intro=NSTextField(wrappingLabelWithString:"One-time setup: install the Chrome extension, connect its ID below, and allow screen access for desktop app screenshots. Move this app to Applications before connecting it.")
        intro.font = .systemFont(ofSize:14)
        idField.placeholderString="Paste the 32-letter Chrome extension ID";idField.font = .monospacedSystemFont(ofSize:13,weight:.regular)
        stack.addArrangedSubview(title);stack.addArrangedSubview(intro);stack.addArrangedSubview(idField)
        stack.addArrangedSubview(button("Connect Chrome extension",#selector(connect)))
        status.textColor = .secondaryLabelColor;stack.addArrangedSubview(status)
        stack.addArrangedSubview(button("Allow Screen Recording…",#selector(screenPermission)))
        stack.addArrangedSubview(button("Optional: allow window restoration…",#selector(accessibility)))
        permissions.font = .systemFont(ofSize:13);permissions.textColor = .secondaryLabelColor;stack.addArrangedSubview(permissions)
        let note=NSTextField(wrappingLabelWithString:"Screen Recording lets Tabline capture desktop windows. Accessibility is optional and only used to restore a minimized window. After changing screen permission, quit and reopen Chrome if macOS requests it. Chrome tabs can still be copied without desktop permission.")
        note.font = .systemFont(ofSize:12);note.textColor = .secondaryLabelColor;stack.addArrangedSubview(note)
        stack.addArrangedSubview(button("Quit companion",#selector(quit)))
        window.contentView?.addSubview(stack)
        NSLayoutConstraint.activate([stack.leadingAnchor.constraint(equalTo:window.contentView!.leadingAnchor),stack.trailingAnchor.constraint(equalTo:window.contentView!.trailingAnchor),stack.topAnchor.constraint(equalTo:window.contentView!.topAnchor),idField.widthAnchor.constraint(equalToConstant:530)])
    }
    private func button(_ title:String,_ action:Selector)->NSButton{let value=NSButton(title:title,target:self,action:action);value.bezelStyle = .rounded;return value}
    func show(){window.center();NSApp.activate(ignoringOtherApps:true);window.makeKeyAndOrderFront(nil);refresh();timer?.invalidate();timer=Timer.scheduledTimer(withTimeInterval:1,repeats:true){[weak self] _ in self?.refresh()}}
    func message(_ text:String){status.stringValue=text}
    private func refresh(){permissions.stringValue="Screen access: \(CGPreflightScreenCaptureAccess() ? "allowed":"not allowed")    ·    Window restoration: \(AXIsProcessTrusted() ? "allowed":"optional / off")"}
    @objc private func connect(){let id=idField.stringValue.trimmingCharacters(in:.whitespacesAndNewlines);guard NativeFraming.extensionID(id) else{message("Paste the extension ID from Tabline’s Chrome settings; it must be 32 letters a–p.");return};do{try register?(id);message("Connected. Return to Chrome and press Option + S.")}catch{message(error.localizedDescription)}}
    @objc private func screenPermission(){if !CGPreflightScreenCaptureAccess(){_ = CGRequestScreenCaptureAccess()};if !CGPreflightScreenCaptureAccess(){NSWorkspace.shared.open(URL(string:"x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture")!)};refresh()}
    @objc private func accessibility(){let key=kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String;_ = AXIsProcessTrustedWithOptions([key:true] as CFDictionary);refresh()}
    @objc private func quit(){NSApp.terminate(nil)}
    func windowWillClose(_ notification:Notification){timer?.invalidate();timer=nil;if standalone{NSApp.terminate(nil)}}
    static func register(_ extensionID:String)throws{
        guard NativeFraming.extensionID(extensionID),let executable=Bundle.main.executableURL,Bundle.main.bundleIdentifier=="com.tabline.companion" else{throw CompanionError.message("Open the packaged Tabline Companion app, not the raw development executable.")}
        let folder=FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/Google/Chrome/NativeMessagingHosts",isDirectory:true)
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
        let target=folder.appendingPathComponent("com.tabline.companion.json")
        let manifest:[String:Any]=["name":"com.tabline.companion","description":"Local Tabline Mac window previews and screenshot clipboard","path":executable.path,"type":"stdio","allowed_origins":["chrome-extension://\(extensionID)/"]]
        try JSONSerialization.data(withJSONObject:manifest,options:[.prettyPrinted,.sortedKeys]).write(to:target,options:.atomic)
    }
}
