import AppKit
import TablineCore

final class PickerPanel:NSPanel {override var canBecomeKey:Bool{true};override var canBecomeMain:Bool{false}}

@MainActor final class RopePanel {
    let panel:PickerPanel
    let view=RopeView(frame:.zero)
    var closed:(()->Void)?
    private var outside:Any?
    private var workspaceObserver:NSObjectProtocol?
    private var sourcePID:pid_t=0
    private(set) var isVisible=false
    private(set) var sourceBounds=CGRect.zero
    init() {
        panel=PickerPanel(contentRect:.zero,styleMask:[.borderless,.nonactivatingPanel],backing:.buffered,defer:false)
        panel.isOpaque=false;panel.backgroundColor = .clear;panel.hasShadow=false;panel.level = .floating
        panel.isFloatingPanel=true;panel.hidesOnDeactivate=false;panel.acceptsMouseMovedEvents=true
        panel.collectionBehavior=[.canJoinAllSpaces,.fullScreenAuxiliary,.transient,.ignoresCycle]
        panel.sharingType = .none;panel.contentView=view
        view.dismiss={ [weak self] in self?.hide() }
    }
    func show(_ cards:[Card],request:[String:Any]) {
        hide(notify:false)
        sourcePID=NSWorkspace.shared.frontmostApplication?.processIdentifier ?? 0
        let b=request["bounds"] as? [String:Any] ?? [:]
        let primary=NSScreen.screens.first?.frame.height ?? 900
        let left=(b["left"] as? Double ?? 0),top=(b["top"] as? Double ?? 0)
        let width=(b["width"] as? Double ?? Double(NSScreen.main?.frame.width ?? 1200)),height=(b["height"] as? Double ?? 800)
        let browser=CGRect(x:left,y:primary-top-height,width:width,height:height)
        sourceBounds=browser
        let screen=NSScreen.screens.max{a,c in a.frame.intersection(browser).width*a.frame.intersection(browser).height < c.frame.intersection(browser).width*c.frame.intersection(browser).height} ?? NSScreen.main!
        var pageTop=browser.maxY-88
        if let viewport=request["viewport"] as? [String:Any],let h=viewport["height"] as? Double,h>0 {
            let ratio=viewport["scale"] as? Double ?? Double(screen.backingScaleFactor)
            pageTop=min(browser.maxY-40,max(browser.maxY-160,browser.minY+8+h*ratio/Double(screen.backingScaleFactor)))
        }
        let x=max(screen.visibleFrame.minX,browser.minX+8),right=min(screen.visibleFrame.maxX,browser.maxX-8)
        let logicalWidth=max(180,right-x),available=max(120,min(pageTop-screen.visibleFrame.minY,height-96))
        let countHint=max(cards.count,request["desktop"] as? Bool != false ? 2:0)
        let provisional=RopeLayout(width:logicalWidth,height:available,count:countHint)
        let pickerHeight=min(available,18+provisional.sag+318*provisional.cardWidth/376+(provisional.compact&&countHint>1 ? 56:0))
        panel.setFrame(CGRect(x:x,y:max(screen.visibleFrame.minY,pageTop-pickerHeight),width:logicalWidth,height:pickerHeight),display:false)
        view.frame=CGRect(origin:.zero,size:panel.frame.size);view.reset(cards)
        isVisible=true;panel.makeKeyAndOrderFront(nil);panel.makeFirstResponder(view)
        outside=NSEvent.addGlobalMonitorForEvents(matching:[.leftMouseDown,.rightMouseDown]){[weak self] _ in
            guard let self=self,self.isVisible,!self.panel.frame.contains(NSEvent.mouseLocation) else{return};self.hide()
        }
        workspaceObserver=NSWorkspace.shared.notificationCenter.addObserver(forName:NSWorkspace.didActivateApplicationNotification,object:nil,queue:.main){[weak self] notice in
            guard let self=self,self.isVisible,let app=notice.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication else{return}
            if app.processIdentifier != self.sourcePID && app.processIdentifier != getpid(){self.hide()}
        }
        view.needsDisplay=true
    }
    func hide(notify:Bool=true) {
        let wasVisible=isVisible;isVisible=false;view.stop();panel.orderOut(nil)
        if let monitor=outside{NSEvent.removeMonitor(monitor);outside=nil}
        if let observer=workspaceObserver{NSWorkspace.shared.notificationCenter.removeObserver(observer);workspaceObserver=nil}
        if wasVisible&&notify{closed?()}
    }
    func update(_ tabs:[Card]) {view.update(tabs)}
}

@MainActor final class RopeView:NSView {
    override var isFlipped:Bool{true}
    override var acceptsFirstResponder:Bool{true}
    var select:((Card,String)->Void)?
    var dismiss:(()->Void)?
    var visibleCards:(([Card])->Void)?
    private(set) var cards:[Card]=[]
    private var hover:String?
    private var initial:String?
    private var openingPointer=NSPoint.zero
    private var hoverAge:Double=0.12
    private var offset:CGFloat=0,target:CGFloat=0,velocity:CGFloat=0
    private var frequency:CGFloat=48
    private var timer:Timer?
    private var lastTick:Double=0
    private var tracking:NSTrackingArea?
    private var pressedArrow=0
    private let decoded=NSCache<NSString,NSImage>()
    var layout:RopeLayout{RopeLayout(width:bounds.width,height:bounds.height,count:cards.count,offset:offset)}
    var animating:Bool{abs(target-offset)>0.01||abs(velocity)>0.01}
    var stats:[String:Any]{["cards":cards.count,"offset":Double(offset),"targetOffset":Double(target),"animating":animating,"cardIds":cards.map{$0.id},"visible":window?.isVisible ?? false]}
    override init(frame:NSRect){super.init(frame:frame);decoded.totalCostLimit=3*1024*1024;decoded.countLimit=12}
    required init?(coder:NSCoder){fatalError("Programmatic view only")}
    func reset(_ values:[Card]) {
        cards=values
        if let index=cards.firstIndex(where:{$0.current}) {let current=cards.remove(at:index);cards.insert(current,at:cards.count/2)}
        let current=cards.firstIndex(where:{$0.current}) ?? 0
        offset=CGFloat(current)*layout.pitch;target=offset;velocity=0;hover=nil;initial=cards.first(where:{$0.current})?.id
        openingPointer=NSEvent.mouseLocation;decoded.removeAllObjects();pressedArrow=0
    }
    func update(_ tabs:[Card]) {
        let anchor=Int((offset/layout.pitch).rounded()),anchorID=cards.indices.contains(anchor) ? cards[anchor].id:nil
        let incoming=Dictionary(tabs.map{($0.id,$0)},uniquingKeysWith:{$1})
        cards.removeAll{$0.windowID==nil&&incoming[$0.id]==nil}
        for value in tabs {
            if let old=cards.first(where:{$0.id==value.id&&$0.windowID==nil}){if old.image != value.image{decoded.removeObject(forKey:old.id as NSString)};old.title=value.title;old.image=value.image;old.icon=value.icon}
            else{cards.append(value)}
        }
        if let id=anchorID,let next=cards.firstIndex(where:{$0.id==id}){let shift=CGFloat(next-anchor)*layout.pitch;offset+=shift;target+=shift}
        offset=min(layout.maximum,max(0,offset));target=min(layout.maximum,max(0,target));needsDisplay=true
    }
    func clearImages(){decoded.removeAllObjects();for card in cards{card.image=nil};needsDisplay=true}
    func appendApps(_ values:[Card]){cards.append(contentsOf:values);needsDisplay=true}
    func stop(){timer?.invalidate();timer=nil;velocity=0;hover=nil;initial=nil;decoded.removeAllObjects();cards.removeAll()}
    override func updateTrackingAreas(){super.updateTrackingAreas();if let area=tracking{removeTrackingArea(area)};tracking=NSTrackingArea(rect:bounds,options:[.mouseMoved,.mouseEnteredAndExited,.activeAlways,.inVisibleRect],owner:self);addTrackingArea(tracking!)}
    private func arrow(_ point:CGPoint)->Int{guard cards.count>1 else{return 0};return layout.arrow(-1).contains(point) ? -1 : layout.arrow(1).contains(point) ? 1:0}
    private func enabled(_ dir:Int)->Bool{dir<0 ? target>0:target<layout.maximum}
    private func hit(_ point:CGPoint)->(Int,CGPoint)? {
        guard layout.viewport.contains(point) else{return nil}
        for index in cards.indices.reversed(){let local=layout.local(point,index:index);if NSBezierPath(roundedRect:CGRect(x:0,y:0,width:376,height:285),xRadius:18,yRadius:18).contains(local){return(index,local)}}
        return nil
    }
    override func mouseMoved(with event:NSEvent){updateHover(convert(event.locationInWindow,from:nil))}
    override func mouseExited(with event:NSEvent){hover=nil;needsDisplay=true;NSCursor.arrow.set()}
    private func updateHover(_ point:CGPoint) {
        if initial != nil&&NSEvent.mouseLocation==openingPointer{return}
        let navigation=arrow(point),match=(!animating&&navigation==0) ? hit(point):nil,id=match.map{cards[$0.0].id}
        if hover != id{hover=id;hoverAge=0;if id != nil{initial=nil};wake();needsDisplay=true}
        if id != nil||(navigation != 0&&enabled(navigation)){NSCursor.pointingHand.set()}else{NSCursor.arrow.set()}
    }
    override func mouseDown(with event:NSEvent) {
        let point=convert(event.locationInWindow,from:nil),direction=arrow(point)
        if direction != 0{pressedArrow=direction;needsDisplay=true;return}
        if let (index,local)=hit(point){select?(cards[index],CGRect(x:193,y:182,width:112,height:47).contains(local) ? "save":"copy")}
        else{dismiss?()}
    }
    override func mouseUp(with event:NSEvent){let old=pressedArrow;pressedArrow=0;let point=convert(event.locationInWindow,from:nil);if old != 0&&arrow(point)==old&&enabled(old){scroll(to:layout.next(old,target:target),arrow:true)};needsDisplay=true}
    override func keyDown(with event:NSEvent){switch event.keyCode{case 53:dismiss?();case 123:scroll(to:layout.next(-1,target:target),arrow:true);case 124:scroll(to:layout.next(1,target:target),arrow:true);default:super.keyDown(with:event)}}
    override func scrollWheel(with event:NSEvent){let delta=abs(event.scrollingDeltaX)>abs(event.scrollingDeltaY) ? -event.scrollingDeltaX:-event.scrollingDeltaY;scroll(to:target+delta*(event.hasPreciseScrollingDeltas ? 1:20),arrow:false)}
    private func scroll(to value:CGFloat,arrow:Bool) {
        initial=nil;hover=nil;target=min(layout.maximum,max(0,value));frequency=arrow ? 32:48
        if velocity*(target-offset)<0{velocity*=0.35}
        if NSWorkspace.shared.accessibilityDisplayShouldReduceMotion{offset=target;velocity=0;needsDisplay=true}else{wake()}
    }
    private func wake(){guard timer==nil else{return};lastTick=ProcessInfo.processInfo.systemUptime;timer=Timer.scheduledTimer(withTimeInterval:1/60,repeats:true){[weak self] _ in self?.tick()};timer?.tolerance=0.002}
    private func tick(){
        let now=ProcessInfo.processInfo.systemUptime,dt=CGFloat(now-lastTick);lastTick=now
        if animating{let x=offset-target,c=velocity+frequency*x,decay=exp(-frequency*dt);let next=target+(x+c*dt)*decay;velocity=(velocity-frequency*c*dt)*decay;offset=min(layout.maximum,max(0,next));if abs(offset-target)<0.4&&abs(velocity)<12{offset=target;velocity=0}}
        hoverAge=min(0.12,hoverAge+Double(dt));needsDisplay=true
        if !animating&&(hover==nil||hoverAge>=0.12){timer?.invalidate();timer=nil;updateHover(convert(window?.mouseLocationOutsideOfEventStream ?? .zero,from:nil))}
    }
    private static func color(_ r:CGFloat,_ g:CGFloat,_ b:CGFloat,_ a:CGFloat=1)->NSColor{NSColor(srgbRed:r/255,green:g/255,blue:b/255,alpha:a)}
    private func text(_ value:String,rect:CGRect,size:CGFloat,color:NSColor,align:NSTextAlignment = .left,weight:NSFont.Weight = .regular) {
        let paragraph=NSMutableParagraphStyle();paragraph.alignment=align;paragraph.lineBreakMode = .byTruncatingTail
        (value as NSString).draw(in:rect,withAttributes:[.font:NSFont.systemFont(ofSize:size,weight:weight),.foregroundColor:color,.paragraphStyle:paragraph])
    }
    private func rounded(_ rect:CGRect,_ radius:CGFloat)->NSBezierPath{NSBezierPath(roundedRect:rect,xRadius:radius,yRadius:radius)}
    private func glass(_ rect:CGRect,radius:CGFloat=18,edge:NSColor?=nil){
        let path=rounded(rect,radius)
        NSGradient(starting:Self.color(34,46,59,0.96),ending:Self.color(18,29,41,0.98))?.draw(in:path,angle:90)
        (edge ?? Self.color(128,153,177,0.6)).setStroke();path.lineWidth=edge==nil ? 1.2:2.3;path.stroke()
        let inset=rounded(rect.insetBy(dx:1.5,dy:1.5),max(1,radius-1.5));Self.color(223,240,255,0.09).setStroke();inset.lineWidth=0.7;inset.stroke()
    }
    private func clip(center:CGFloat,y:CGFloat,scale:CGFloat) {
        let r=CGRect(x:center-8*scale,y:y,width:16*scale,height:37*scale),path=rounded(r,1.5*scale)
        NSGradient(colorsAndLocations:(Self.color(71,88,111),0),(Self.color(224,234,247),0.13),(Self.color(156,175,198),0.45),(Self.color(224,234,247),0.87),(Self.color(79,99,124),1))?.draw(in:path,angle:0)
        Self.color(227,239,252,0.6).setStroke();path.lineWidth=0.7*scale;path.stroke()
        Self.color(249,252,255).setFill();rounded(CGRect(x:r.minX+0.5*scale,y:y,width:r.width-scale,height:3*scale),scale).fill()
        Self.color(72,91,115).setFill();CGRect(x:r.minX+scale,y:y+4*scale,width:r.width-2*scale,height:4*scale).fill()
        Self.color(45,61,82).setFill();NSBezierPath(ovalIn:CGRect(x:center-2*scale,y:y+27*scale,width:4*scale,height:5*scale)).fill()
    }
    private func actions(_ opacity:CGFloat) {
        NSGraphicsContext.saveGraphicsState();NSGraphicsContext.current?.cgContext.setAlpha(opacity);defer{NSGraphicsContext.restoreGraphicsState()}
        for save in [false,true]{let r=CGRect(x:save ? 193:72,y:182,width:save ? 112:114,height:47)
            glass(r,radius:23.5,edge:Self.color(149,179,194,opacity*0.7))
            let icon=NSBezierPath();icon.lineWidth=1.8;icon.lineCapStyle = .round;icon.lineJoinStyle = .round
            let x=r.minX+21,cy=r.midY
            if save{icon.move(to:CGPoint(x:x+9,y:cy-10));icon.line(to:CGPoint(x:x+9,y:cy+3));icon.move(to:CGPoint(x:x+4,y:cy-2));icon.line(to:CGPoint(x:x+9,y:cy+3));icon.line(to:CGPoint(x:x+14,y:cy-2));icon.move(to:CGPoint(x:x,y:cy+4));icon.line(to:CGPoint(x:x,y:cy+10));icon.line(to:CGPoint(x:x+19,y:cy+10));icon.line(to:CGPoint(x:x+19,y:cy+4))}
            else{icon.append(rounded(CGRect(x:x,y:cy-11,width:14,height:17),1.5));icon.append(rounded(CGRect(x:x+5,y:cy-7,width:14,height:17),1.5))}
            Self.color(234,242,250,opacity).setStroke();icon.stroke();text(save ? "Save":"Copy",rect:CGRect(x:x+29,y:r.minY+11,width:60,height:27),size:20,color:Self.color(235,244,252,opacity))
        }
    }
    override func draw(_ dirtyRect:NSRect){
        NSGraphicsContext.current?.imageInterpolation = .high
        let l=layout
        let line=NSBezierPath();line.move(to:CGPoint(x:0,y:l.cable(0)))
        for x in stride(from:CGFloat(2),through:bounds.width,by:2){line.line(to:CGPoint(x:x,y:l.cable(x)))}
        Self.color(65,85,110).setStroke();line.lineWidth=5;line.stroke();Self.color(164,183,206).setStroke();line.lineWidth=3;line.stroke()
        let bright=NSBezierPath();bright.move(to:CGPoint(x:0,y:l.cable(0)-1));for x in stride(from:CGFloat(2),through:bounds.width,by:2){bright.line(to:CGPoint(x:x,y:l.cable(x)-1))};Self.color(242,248,255,0.96).setStroke();bright.lineWidth=1;bright.stroke()
        NSGraphicsContext.saveGraphicsState();NSBezierPath(rect:l.viewport).addClip()
        var onScreen:[Card]=[]
        for (index,card) in cards.enumerated(){
            let r=l.rect(index),k=r.width/376
            if r.maxX<l.viewport.minX-8||r.minX>l.viewport.maxX+8{decoded.removeObject(forKey:card.id as NSString);if card.windowID != nil{card.image=nil};continue}
            onScreen.append(card)
            NSGraphicsContext.saveGraphicsState()
            let transform=NSAffineTransform();transform.translateX(by:r.midX,yBy:r.minY+4.5*k);transform.rotate(byDegrees:l.roll(index));transform.scale(by:k);transform.translateX(by:-188,yBy:-4.5);transform.concat()
            let highlighted=hover==card.id||initial==card.id
            let alpha:CGFloat=NSWorkspace.shared.accessibilityDisplayShouldReduceMotion ? 1:CGFloat(1-pow(1-min(1,hoverAge/0.1),3))
            glass(CGRect(x:0,y:0,width:376,height:285),edge:highlighted ? Self.color(86,186,252,initial==card.id ? 1:alpha):nil)
            let picture=CGRect(x:13,y:14,width:350,height:218)
            var image=decoded.object(forKey:card.id as NSString)
            if image==nil,let data=card.image,let loaded=NSImage(data:data){image=loaded;decoded.setObject(loaded,forKey:card.id as NSString,cost:360*220*4)}
            if let image=image{NSGraphicsContext.saveGraphicsState();rounded(picture,9).addClip();let factor=max(picture.width/image.size.width,picture.height/image.size.height);let dest=CGRect(x:picture.midX-image.size.width*factor/2,y:picture.midY-image.size.height*factor/2,width:image.size.width*factor,height:image.size.height*factor);image.draw(in:dest,from:.zero,operation:.sourceOver,fraction:1,respectFlipped:true,hints:nil);NSGraphicsContext.restoreGraphicsState()}
            else{drawIcon(card,rect:CGRect(x:157,y:34,width:62,height:62));text(card.title,rect:CGRect(x:24,y:109,width:328,height:30),size:20,color:Self.color(237,244,251),align:.center);text(card.domain,rect:CGRect(x:24,y:144,width:328,height:25),size:16,color:Self.color(159,184,207),align:.center);if hover != card.id{text("Capture on click",rect:CGRect(x:24,y:187,width:328,height:25),size:16,color:Self.color(184,204,224),align:.center)}}
            drawIcon(card,rect:CGRect(x:21,y:240,width:29,height:33));text(card.title,rect:CGRect(x:61,y:244,width:295,height:32),size:20,color:Self.color(235,243,251))
            if hover==card.id{actions(alpha)}
            NSGraphicsContext.restoreGraphicsState();clip(center:r.midX,y:l.cable(r.midX)-6*k,scale:k)
        }
        NSGraphicsContext.restoreGraphicsState()
        if cards.count>1{for dir in [-1,1]{let targetRect=l.arrow(dir),r=targetRect.insetBy(dx:pressedArrow==dir ? 4.8:4,dy:pressedArrow==dir ? 4.8:4);glass(r,radius:r.width/2);let arrow=NSBezierPath();arrow.move(to:CGPoint(x:r.midX-CGFloat(dir)*3,y:r.midY-6));arrow.line(to:CGPoint(x:r.midX+CGFloat(dir)*3,y:r.midY));arrow.line(to:CGPoint(x:r.midX-CGFloat(dir)*3,y:r.midY+6));arrow.lineWidth=1.8;arrow.lineCapStyle = .round;arrow.lineJoinStyle = .round;Self.color(231,243,255,enabled(dir) ? 1:0.28).setStroke();arrow.stroke()}}
        // Schedule after painting, never start a screenshot job for every frame.
        if !onScreen.isEmpty{let values=onScreen;DispatchQueue.main.async{[weak self] in guard self?.window?.isVisible==true else{return};self?.visibleCards?(values)}}
    }
    private func drawIcon(_ card:Card,rect:CGRect){if let icon=card.icon{icon.draw(in:rect,from:.zero,operation:.sourceOver,fraction:1,respectFlipped:true,hints:nil)}else{Self.color(60,127,195).setFill();rounded(rect,min(8,rect.width/4)).fill();let box=rect.insetBy(dx:rect.width*0.23,dy:rect.height*0.25);Self.color(233,244,255).setStroke();let path=rounded(box,1.5);path.lineWidth=1.2;path.stroke()}}
}
