import AppKit
import CoreServices
import TablineCore

@MainActor enum MacInstaller {
    static var destination:URL {FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Applications/Tabline Companion.app",isDirectory:true)}
    static var installed:Bool {Bundle.main.bundleURL.standardizedFileURL == destination.standardizedFileURL}
    static func install(extensionID:String?,completion:@escaping (String?)->Void)throws {
        let files=FileManager.default,source=Bundle.main.bundleURL,target=destination
        guard source.pathExtension=="app",Bundle.main.bundleIdentifier=="com.tabline.companion" else{throw CompanionError.message("Open Tabline Companion from the downloaded disk image.")}
        guard !installed else{return}
        if NSWorkspace.shared.runningApplications.contains(where:{$0.bundleIdentifier=="com.tabline.companion" && $0.processIdentifier != getpid()}) {
            throw CompanionError.message("Quit Chrome and the previous Tabline companion before updating, then choose Install Tabline again.")
        }
        let parent=target.deletingLastPathComponent()
        try files.createDirectory(at:parent,withIntermediateDirectories:true)
        guard parent.resolvingSymlinksInPath().path.hasPrefix(files.homeDirectoryForCurrentUser.resolvingSymlinksInPath().path+"/") else{throw CompanionError.message("Your Applications folder points outside your home folder. Install Tabline manually with your administrator.")}
        if files.fileExists(atPath:target.path) {
            let backup=parent.appendingPathComponent("Tabline Companion.previous.\(UUID().uuidString).app")
            try files.moveItem(at:target,to:backup)
        }
        try files.copyItem(at:source,to:target)
        LSRegisterURL(target as CFURL,true)
        let config=NSWorkspace.OpenConfiguration();config.createsNewApplicationInstance=true
        config.arguments=extensionID.map{["--connect-extension=\($0)"]} ?? []
        NSWorkspace.shared.openApplication(at:target,configuration:config){_,error in
            Task{@MainActor in if let error=error{completion(error.localizedDescription)}else{NSApp.terminate(nil)}}
        }
    }
}
