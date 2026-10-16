import AppKit

@MainActor final class Toast {
    private let panel=PickerPanel(contentRect:.zero,styleMask:[.borderless,.nonactivatingPanel],backing:.buffered,defer:false)
    private let view=ToastView(frame:.zero)
    private var timer:Timer?
    private var began=0.0
    private var pending=false
    init(){panel.isOpaque=false;panel.backgroundColor = .clear;panel.hasShadow=false;panel.level = .statusBar;panel.ignoresMouseEvents=true;panel.sharingType = .none;panel.collectionBehavior=[.canJoinAllSpaces,.fullScreenAuxiliary,.transient];panel.contentView=view}
    func show(_ text:String,success:Bool,pending:Bool=false,bounds:CGRect?=nil){
        hide();self.pending=pending;view.text=text;view.success=success;view.pending=pending
        let screen=NSScreen.screens.first{ $0.frame.contains(bounds.map{CGPoint(x:$0.midX,y:$0.midY)} ?? NSEvent.mouseLocation) } ?? NSScreen.main!
        var area=(bounds ?? screen.visibleFrame).intersection(screen.visibleFrame)
        if area.isNull||area.width<100||area.height<100{area=screen.visibleFrame}
        let width:CGFloat=297,height:CGFloat=78
        let x=min(screen.visibleFrame.maxX-width,max(screen.visibleFrame.minX,area.midX-width/2))
        let y=max(screen.visibleFrame.minY,area.minY+42)
        panel.setFrame(CGRect(x:x,y:y,width:width,height:height),display:false);view.frame=CGRect(x:0,y:0,width:width,height:height)
        began=ProcessInfo.processInfo.systemUptime;panel.alphaValue=0;panel.orderFrontRegardless()
        timer=Timer.scheduledTimer(withTimeInterval:pending ? 0.06:0.03,repeats:true){[weak self] _ in MainActor.assumeIsolated { self?.tick() }}
    }
    private func tick(){let elapsed=ProcessInfo.processInfo.systemUptime-began;if elapsed>(pending ? 10:2.9){hide();return};panel.alphaValue=elapsed<0.14 ? elapsed/0.14:(!pending&&elapsed>2.6 ? (2.9-elapsed)/0.3:1);if pending{view.angle=elapsed*300;view.needsDisplay=true}}
    func hide(){timer?.invalidate();timer=nil;panel.orderOut(nil)}
}

final class ToastView:NSView {
    override var isFlipped:Bool{true}
    var text="",success=true,pending=false,angle=0.0
    override func draw(_ dirtyRect:NSRect){
        let box=CGRect(x:14,y:8,width:269,height:54),shape=NSBezierPath(roundedRect:box,xRadius:27,yRadius:27)
        NSGradient(starting:NSColor(srgbRed:0.14,green:0.18,blue:0.22,alpha:0.97),ending:NSColor(srgbRed:0.08,green:0.12,blue:0.17,alpha:0.98))?.draw(in:shape,angle:90)
        NSColor(srgbRed:success||pending ? 0.43:0.75,green:success||pending ? 0.51:0.30,blue:success||pending ? 0.59:0.29,alpha:0.65).setStroke();shape.lineWidth=1.25;shape.stroke()
        let inset=NSBezierPath(roundedRect:box.insetBy(dx:1.3,dy:1.3),xRadius:26,yRadius:26);NSColor.white.withAlphaComponent(0.12).setStroke();inset.lineWidth=0.7;inset.stroke()
        if pending{let path=NSBezierPath();path.appendArc(withCenter:CGPoint(x:48,y:35),radius:12.5,startAngle:angle,endAngle:angle+272,clockwise:false);path.lineWidth=1.9;path.lineCapStyle = .round;NSColor(calibratedWhite:0.9,alpha:1).setStroke();path.stroke()}
        else{let icon=NSBezierPath(ovalIn:CGRect(x:34.5,y:21.5,width:27,height:27));(success ? NSColor(srgbRed:0.42,green:0.88,blue:0.35,alpha:1):NSColor(srgbRed:0.96,green:0.35,blue:0.31,alpha:1)).setFill();icon.fill();let mark=NSBezierPath();mark.lineWidth=2.35;mark.lineCapStyle = .round;mark.lineJoinStyle = .round;if success{mark.move(to:CGPoint(x:42.5,y:35));mark.line(to:CGPoint(x:46.7,y:39.2));mark.line(to:CGPoint(x:54,y:30.8))}else{mark.move(to:CGPoint(x:43.8,y:30.8));mark.line(to:CGPoint(x:52.2,y:39.2));mark.move(to:CGPoint(x:52.2,y:30.8));mark.line(to:CGPoint(x:43.8,y:39.2))};NSColor(srgbRed:0.1,green:0.2,blue:0.15,alpha:1).setStroke();mark.stroke()}
        let paragraph=NSMutableParagraphStyle();paragraph.lineBreakMode = .byTruncatingTail
        (text as NSString).draw(in:CGRect(x:82,y:23,width:190,height:28),withAttributes:[.font:NSFont.systemFont(ofSize:20),.foregroundColor:NSColor(srgbRed:0.9,green:0.93,blue:0.96,alpha:1),.paragraphStyle:paragraph])
    }
}
