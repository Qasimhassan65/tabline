import AppKit

let application=NSApplication.shared
let delegate=Companion()
application.setActivationPolicy(.accessory)
application.delegate=delegate
application.run()
