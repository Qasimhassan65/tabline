import AppKit
import ScreenCaptureKit
import ApplicationServices
import ImageIO
import UniformTypeIdentifiers
import TablineCore

final class Card {
    let id:String
    var title:String
    var app:String
    var domain:String
    var image:Data?
    var icon:NSImage?
    let windowID:CGWindowID?
    var owner:pid_t?
    var current:Bool
    init(id:String,title:String,app:String,domain:String="",image:Data?=nil,icon:NSImage?=nil,windowID:CGWindowID?=nil,owner:pid_t?=nil,current:Bool=false) {
        self.id=id;self.title=title;self.app=app;self.domain=domain;self.image=image;self.icon=icon;self.windowID=windowID;self.owner=owner;self.current=current
    }
    static func dataURL(_ text:String?) -> Data? {
        guard let text=text,text.utf8.count<1_500_000,let comma=text.firstIndex(of:",") else{return nil}
        return Data(base64Encoded:String(text[text.index(after:comma)...]))
    }
    static func tabs(_ request:[String:Any])->[Card] {
        (request["tabs"] as? [[String:Any]] ?? []).prefix(1000).compactMap { row in
            guard let id=row["id"] as? String,id.count<80 else{return nil}
            let icon=dataURL(row["icon"] as? String).flatMap{NSImage(data:$0)}
            return Card(id:id,title:String((row["title"] as? String ?? "Untitled tab").prefix(512)),app:"Chrome",domain:row["domain"] as? String ?? "Chrome",image:dataURL(row["image"] as? String),icon:icon,current:row["current"] as? Bool ?? false)
        }
    }
}

@MainActor final class CaptureService {
    let cache=PreviewCache()
    private var known:[CGWindowID:SCWindow]=[:]
    private var previewTasks:[String:Task<Void,Never>]=[:]
    private var previewAttempts=Set<String>()
    private var generation=0
    private var captureBusy=false
    private(set) var captureCount=0
    var screenPermission:Bool {CGPreflightScreenCaptureAccess()}
    var accessibilityPermission:Bool {AXIsProcessTrusted()}
    var permissionInfo:[String:Any] {["screenRecording":screenPermission,"accessibility":accessibilityPermission]}

    func windows() async throws -> [Card] {
        guard screenPermission else {return []}
        let content=try await SCShareableContent.excludingDesktopWindows(true,onScreenWindowsOnly:false)
        known.removeAll(keepingCapacity:true)
        return content.windows.sorted{$0.windowID<$1.windowID}.compactMap { window in
            guard let app=window.owningApplication,app.processID != getpid(),
                  app.bundleIdentifier != "com.google.Chrome",window.windowLayer==0,
                  window.frame.width>=100,window.frame.height>=60,
                  let running=NSRunningApplication(processIdentifier:app.processID),running.activationPolicy == .regular else{return nil}
            known[window.windowID]=window
            let key="app:\(window.windowID)",name=app.applicationName
            return Card(id:key,title:(window.title?.isEmpty==false ? window.title! : name),app:name,domain:name,image:cache.get(key),icon:running.icon,windowID:window.windowID,owner:app.processID)
        }
    }
    func refreshWindow(_ id:CGWindowID,owner:pid_t?)async throws->SCWindow {
        guard screenPermission else{throw CompanionError.message("Allow Screen Recording for Tabline Companion in System Settings, then reconnect.")}
        let content=try await SCShareableContent.excludingDesktopWindows(true,onScreenWindowsOnly:false)
        guard let window=content.windows.first(where:{$0.windowID==id}),let app=window.owningApplication,owner==nil||owner==app.processID else {
            throw CompanionError.message("This app window has closed or is unavailable.")
        }
        known[id]=window;return window
    }
    private func image(_ window:SCWindow,thumbnail:Bool)async throws->CGImage {
        let filter=SCContentFilter(desktopIndependentWindow:window)
        let config=SCStreamConfiguration()
        let scale=max(1,CGFloat(filter.pointPixelScale)),rect=filter.contentRect
        let width=max(1,rect.width),height=max(1,rect.height)
        let ratio=thumbnail ? min(360/width,220/height,1) : scale
        let pixelWidth=Int((width*ratio).rounded(.up)),pixelHeight=Int((height*ratio).rounded(.up))
        guard pixelWidth<=16000,pixelHeight<=16000,Int64(pixelWidth)*Int64(pixelHeight)<=32_000_000 else{
            throw CompanionError.message("Screenshot exceeds the pixel limit.")
        }
        config.width=pixelWidth;config.height=pixelHeight;config.showsCursor=false
        config.capturesAudio=false;config.captureResolution = .best
        config.ignoreShadowsSingleWindow=true
        config.ignoreGlobalClipSingleWindow=true
        config.colorSpaceName=CGColorSpace.sRGB
        return try await SCScreenshotManager.captureImage(contentFilter:filter,configuration:config)
    }
    func requestPreviews(_ cards:[Card],updated:@escaping ()->Void) {
        guard screenPermission,!captureBusy else{return}
        let epoch=generation
        for card in cards where card.windowID != nil && card.image == nil {
            if let cached=cache.get(card.id){card.image=cached;updated();continue}
            guard previewTasks.count<2,previewTasks[card.id]==nil,!previewAttempts.contains(card.id),let window=known[card.windowID!] else{continue}
            previewAttempts.insert(card.id)
            previewTasks[card.id]=Task { [weak self,weak card] in
                guard let self=self,let card=card else{return}
                defer{self.previewTasks.removeValue(forKey:card.id);if epoch==self.generation{updated()}}
                do {
                    let image=try await self.image(window,thumbnail:true)
                    guard !Task.isCancelled,epoch==self.generation else{return}
                    let bytes=try Self.encode(image,type:UTType.jpeg.identifier,quality:0.60)
                    self.cache.put(card.id,bytes);card.image=bytes
                } catch { /* Optional thumbnail; the capture target stays clickable. */ }
            }
        }
    }
    func cancelPreviews() {generation+=1;for task in previewTasks.values{task.cancel()};previewTasks.removeAll();previewAttempts.removeAll()}
    func clear() {cancelPreviews();cache.clear()}

    func capture(_ card:Card)async throws->CGImage {
        guard !captureBusy else{throw CompanionError.message("Another screenshot is being copied.")}
        guard let id=card.windowID else{throw CompanionError.message("Invalid app window.")}
        captureBusy=true;cancelPreviews();defer{captureBusy=false}
        var window=try await refreshWindow(id,owner:card.owner)
        do {let result=try await image(window,thumbnail:false);captureCount+=1;return result}
        catch {
            // SCK captures covered windows directly. Restore only when the target
            // is unavailable to the compositor, rather than raising every app.
            guard !window.isOnScreen else{throw error}
            try restore(window)
            try await Task.sleep(nanoseconds:160_000_000)
            window=try await refreshWindow(id,owner:card.owner)
            let result=try await image(window,thumbnail:false);captureCount+=1;return result
        }
    }
    private func restore(_ window:SCWindow)throws {
        guard accessibilityPermission else{throw CompanionError.message("Restore this window first, or allow Tabline Accessibility access for automatic restoration.")}
        guard let owner=window.owningApplication else{throw CompanionError.message("The app is unavailable.")}
        let app=AXUIElementCreateApplication(owner.processID)
        var value:CFTypeRef?
        guard AXUIElementCopyAttributeValue(app,kAXWindowsAttribute as CFString,&value) == .success,
              let windows=value as? [AXUIElement] else{throw CompanionError.message("Could not restore the selected app window.")}
        let matches=windows.filter{element in
            var title:CFTypeRef?;AXUIElementCopyAttributeValue(element,kAXTitleAttribute as CFString,&title)
            return (title as? String)==window.title
        }
        guard matches.count==1,let target=matches.first else{throw CompanionError.message("Restore this window manually; several windows have the same title.")}
        AXUIElementSetAttributeValue(target,kAXMinimizedAttribute as CFString,kCFBooleanFalse)
        NSRunningApplication(processIdentifier:owner.processID)?.activate(options:[.activateIgnoringOtherApps])
        guard AXUIElementPerformAction(target,kAXRaiseAction as CFString) == .success else{throw CompanionError.message("The selected window could not be raised.")}
    }
    func browserImage(_ request:[String:Any])async throws->CGImage {
        guard screenPermission else{throw CompanionError.message("Screen Recording permission is required for restricted Chrome pages.")}
        let title=request["title"] as? String ?? ""
        let content=try await SCShareableContent.excludingDesktopWindows(true,onScreenWindowsOnly:false)
        let candidates=content.windows.filter{$0.owningApplication?.bundleIdentifier=="com.google.Chrome"&&($0.title?.hasPrefix(title)==true)&&!title.isEmpty&&$0.windowLayer==0}
        guard candidates.count==1,let window=candidates.first else{throw CompanionError.message("The Chrome window changed or could not be identified safely.")}
        let image=try await self.image(window,thumbnail:false)
        guard let viewport=request["viewport"] as? [String:Any],let height=viewport["height"] as? Double,height>0 else{
            throw CompanionError.message("Open a regular webpage first so Tabline can measure Chrome's content area, then retry this restricted page.")
        }
        let ratio=(viewport["scale"] as? Double ?? 1),requestedHeight=Int((height*ratio).rounded())
        let requestedWidth=Int(((viewport["width"] as? Double ?? Double(image.width)/ratio)*ratio).rounded())
        guard requestedHeight<=image.height,requestedWidth<=image.width,requestedHeight>0,requestedWidth>0 else{throw CompanionError.message("Chrome's viewport changed before capture. Try again.")}
        let border=max(0,(image.width-requestedWidth)/2)
        // Chrome viewport is below the toolbar. Validate all crop dimensions;
        // never guess a toolbar height on restricted pages.
        guard let cropped=image.cropping(to:CGRect(x:border,y:image.height-requestedHeight,width:requestedWidth,height:requestedHeight)) else{throw CompanionError.message("Could not crop the Chrome viewport.")}
        captureCount+=1;return cropped
    }
    static func decodePNG(_ bytes:Data)throws->CGImage {
        guard let source=CGImageSourceCreateWithData(bytes as CFData,nil),let info=CGImageSourceCopyPropertiesAtIndex(source,0,nil) as? [CFString:Any],
              let width=info[kCGImagePropertyPixelWidth] as? Int,let height=info[kCGImagePropertyPixelHeight] as? Int,
              width>0,height>0,width<=16000,height<=16000,Int64(width)*Int64(height)<=32_000_000,
              let image=CGImageSourceCreateImageAtIndex(source,0,nil) else{throw CompanionError.message("Invalid PNG dimensions or image data.")}
        return image
    }
    static func encode(_ image:CGImage,type:String=UTType.png.identifier,quality:Double=1)throws->Data {
        let data=NSMutableData()
        guard let dest=CGImageDestinationCreateWithData(data,type as CFString,1,nil) else{throw CompanionError.message("Could not encode the screenshot.")}
        CGImageDestinationAddImage(dest,image,[kCGImageDestinationLossyCompressionQuality:quality] as CFDictionary)
        guard CGImageDestinationFinalize(dest) else{throw CompanionError.message("Could not finish the screenshot image.")}
        return data as Data
    }
}
