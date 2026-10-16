// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "TablineMac",
    platforms: [.macOS(.v14)],
    products: [.executable(name: "TablineCompanion", targets: ["TablineMac"])],
    targets: [
        .target(name: "TablineCore"),
        .executableTarget(name: "TablineMac", dependencies: ["TablineCore"], linkerSettings: [
            .linkedFramework("AppKit"), .linkedFramework("ScreenCaptureKit"),
            .linkedFramework("ApplicationServices"), .linkedFramework("ImageIO")
        ]),
        .testTarget(name: "TablineCoreTests", dependencies: ["TablineCore"])
    ]
)
