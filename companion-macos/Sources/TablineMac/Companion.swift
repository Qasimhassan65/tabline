import AppKit
import ScreenCaptureKit
import UniformTypeIdentifiers
import TablineCore

@MainActor final class Companion:NSObject,NSApplicationDelegate {
    let native=NativeHost(),capture=CaptureService(),rope=RopePanel(),toast=Toast(),setup=SetupWindow()
    private var generation=0
    private var busy=false
    private var pending:Task<Void,Never>?
    private var noticeBounds:CGRect?
    private let isNative=CommandLine.arguments.contains{$0.hasPrefix("chrome-extension://")}
    func applicationDidFinishLaunching(_ notification:Notification){
        setup.register={try SetupWindow.register($0)};setup.standalone = !isNative
        rope.closed={ [weak self] in guard let self=self else{return};self.generation+=1;self.capture.cancelPreviews();self.native.send(["event":"closed"]) }
        rope.view.select={ [weak self] card,operation in self?.select(card,operation:operation) }
        rope.view.visibleCards={ [weak self] cards in guard let self=self else{return};self.capture.requestPreviews(cards){[weak self] in self?.rope.view.needsDisplay=true} }
        if isNative{native.start(receive:{[weak self] request in Task{@MainActor in await self?.reply(request)}},closed:{NSApp.terminate(nil)})}
        else{if let argument=CommandLine.arguments.first(where:{$0.hasPrefix("--connect-extension=")}){setup.requestConnection(String(argument.dropFirst("--connect-extension=".count)))}else{setup.show()}}
    }
    func application(_ application:NSApplication,open urls:[URL]){
        guard let url=urls.first,url.scheme=="tabline",url.host=="connect",let parts=URLComponents(url:url,resolvingAgainstBaseURL:false),let id=parts.queryItems?.first(where:{$0.name=="extension"})?.value else{return}
        setup.requestConnection(id)
    }
    func applicationWillTerminate(_ notification:Notification){pending?.cancel();capture.cancelPreviews();rope.hide(notify:false);toast.hide()}
    private func reply(_ request:[String:Any])async{
        let id=request["requestId"] ?? NSNull()
        do {var result=try await dispatch(request);result["requestId"]=id;result["ok"]=true;native.send(result)}
        catch{pending?.cancel();toast.hide();if ["clipboard","copy","browser-copy"].contains(request["action"] as? String ?? ""){toast.show("Screenshot failed",success:false,bounds:noticeBounds)};native.send(["requestId":id,"ok":false,"error":error.localizedDescription])}
    }
    private func startPending(_ operation:String){pending?.cancel();pending=Task{[weak self] in try? await Task.sleep(nanoseconds:350_000_000);guard !Task.isCancelled,let self=self else{return};self.toast.show(operation=="save" ? "Saving…":"Capturing…",success:false,pending:true,bounds:self.noticeBounds)}}
    private func select(_ card:Card,operation:String){
        guard !busy else{return};rope.hide();toast.hide();startPending(operation)
        if card.windowID==nil{native.send(["event":"copy-tab","id":card.id,"operation":operation]);return}
        busy=true
        Task{[weak self] in guard let self=self else{return};defer{self.busy=false}
            do{let started=Date();let image=try await self.capture.capture(card);guard Date().timeIntervalSince(started)<9 else{throw CompanionError.message("Capture timed out. Try again.")};_ = try self.finish(image,save:operation=="save")}
            catch{self.pending?.cancel();self.toast.show("Screenshot failed",success:false,bounds:self.noticeBounds);self.native.send(["event":"capture-error","error":error.localizedDescription])}
        }
    }
    private func dispatch(_ request:[String:Any])async throws->[String:Any]{
        let action=request["action"] as? String ?? ""
        switch action {
        case "ping":return ["version":"2.3.1","platform":"mac","protocolVersion":1,"permissions":capture.permissionInfo,"capabilities":["rope","desktop-windows","copy","save","permissions","on-demand-previews","guided-setup"]]
        case "setup":setup.show();return ["permissions":capture.permissionInfo]
        case "show":
            guard !busy else{throw CompanionError.message("Wait for the current screenshot or save dialog to finish.")}
            pending?.cancel();toast.hide();generation+=1;let epoch=generation
            rope.show(Card.tabs(request),request:request);noticeBounds=rope.sourceBounds
            if request["desktop"] as? Bool != false,capture.screenPermission {
                Task{[weak self] in guard let self=self else{return};do{let windows=try await self.capture.windows();guard self.rope.isVisible,self.generation==epoch else{return};self.rope.view.appendApps(windows)}catch{NativeHost.log(error.localizedDescription)}}
            }
            return ["permissions":capture.permissionInfo]
        case "hide":rope.hide();return [:]
        case "update-tabs":if rope.isVisible{rope.update(Card.tabs(request))};return [:]
        case "notice":pending?.cancel();toast.show(request["success"] as? Bool == true ? (request["text"] as? String ?? "Screenshot copied"):"Screenshot failed",success:request["success"] as? Bool ?? false,bounds:noticeBounds);return [:]
        case "clear":capture.clear();rope.view.clearImages();return [:]
        case "warm":return [:]
        case "stats":var result=rope.view.stats;result["thumbBytes"]=capture.cache.bytes;result["frameBytes"]=0;result["captureCount"]=capture.captureCount;result["platform"]="mac";return result
        case "list":let windows=try await capture.windows();return ["windows":windows.map{["handle":String($0.windowID!),"title":$0.title,"appName":$0.app]}]
        case "clipboard":
            guard !busy else{throw CompanionError.message("Another screenshot is being copied.")};busy=true;defer{busy=false}
            let bytes=try NativeFraming.png(request["dataUrl"] as? String ?? "")
            let image=try CaptureService.decodePNG(bytes)
            return ["cancelled":try !finish(image,save:request["operation"] as? String == "save",png:bytes)]
        case "browser-copy":
            guard !busy else{throw CompanionError.message("Another screenshot is being copied.")};busy=true;defer{busy=false};rope.hide();toast.hide();startPending(request["operation"] as? String ?? "copy")
            let started=Date(),image=try await capture.browserImage(request)
            guard Date().timeIntervalSince(started)<9 else{throw CompanionError.message("Capture timed out. Try again.")}
            return ["cancelled":try !finish(image,save:request["operation"] as? String == "save")]
        default:throw CompanionError.message("Unknown companion action.")
        }
    }
    private func finish(_ image:CGImage,save:Bool,png:Data?=nil)throws->Bool{
        pending?.cancel();toast.hide();let bytes=try png ?? CaptureService.encode(image)
        if save {
            let previous=NSWorkspace.shared.frontmostApplication
            let dialog=NSSavePanel();dialog.title="Save screenshot";dialog.allowedContentTypes=[.png];dialog.canCreateDirectories=true;dialog.nameFieldStringValue="Tabline-\(ISO8601DateFormatter().string(from:Date()).replacingOccurrences(of:":",with:"-" )).png"
            NSApp.activate(ignoringOtherApps:true)
            defer{previous?.activate(options:[.activateIgnoringOtherApps])}
            guard dialog.runModal() == .OK,let url=dialog.url else{return false}
            try bytes.write(to:url,options:.atomic)
        } else {
            NSPasteboard.general.clearContents()
            guard NSPasteboard.general.setData(bytes,forType:.png) else{throw CompanionError.message("macOS could not write the screenshot to the clipboard.")}
        }
        toast.show(save ? "Screenshot saved":"Screenshot copied",success:true,bounds:noticeBounds);return true
    }
}
