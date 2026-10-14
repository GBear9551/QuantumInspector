"""Build the customer manual; development tooling, excluded from the asset package."""
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / '.release-tools'))
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, PageBreak, KeepTogether
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.colors import HexColor
from reportlab.lib.enums import TA_LEFT
from xml.sax.saxutils import escape
import shutil
import pymupdf as fitz
from pypdf import PdfReader

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'Assets/FightSongStudio/QuantumInspector/Documentation'
DEST.mkdir(parents=True, exist_ok=True)
OUT = ROOT / 'output/pdf'
OUT.mkdir(parents=True, exist_ok=True)
styles = getSampleStyleSheet()
styles.add(ParagraphStyle(name='TitleQI', fontName='Helvetica-Bold', fontSize=28, leading=33, textColor=HexColor('#12344b'), spaceAfter=18))
styles.add(ParagraphStyle(name='SectionQI', fontName='Helvetica-Bold', fontSize=13, leading=18, textColor=HexColor('#12677c'), spaceBefore=15, spaceAfter=6))
styles.add(ParagraphStyle(name='BodyQI', fontName='Helvetica', fontSize=10.5, leading=15, textColor=HexColor('#233747'), spaceAfter=8))

pages = [
('Start here', [
('Quantum Inspector | Version 1.0.0', 'An Editor tool for exploring scene objects, components, project assets and their relationships through an Orbital Wheel and a clustered graph. Developed by Gary Lougheed & Astra.'),
('Requirements and installation', 'Tested in Unity 6000.3.7f1 on Windows. Other Unity versions and operating systems have not been verified. Import the tool scripts, settings and styles together, retaining their .meta files. Wait for compilation to finish before opening the window. The core uses Unity Editor APIs and has no external service, account or API key requirement. Unity CLI, Pipeline and Asset Store Tools are development tools, not customer dependencies.'),
('Demo dependency', 'The Living Laboratory demo and its Editor assembly require Universal RP. This project uses com.unity.render-pipelines.universal 17.3.0. Install the compatible Universal RP package through Unity Package Manager before importing the demo. Use a URP project for its materials and lighting. The tool does not install or change render pipelines for you. Do not claim Built-in or HDRP demo compatibility.'),
('Open and explore', 'Choose Quantum Inspector > Open. Select a GameObject in the native Hierarchy. Use Wheel or Graph to switch presentations, then select a node to explore. Project and Hierarchy select the browsing scope; Parent and Roots navigate upward. Back and Forward revisit navigation history.'),
('Support', 'Email gbear9551@gmail.com. Include your Unity version, operating system, render pipeline, steps to reproduce and relevant Console messages. Remove private source code, credentials and unrelated project data before sending a reproduction.')]),
('Orbital Wheel', [
('Navigate the wheel', 'Scroll over the wheel to rotate its visible nodes. Select an object or component to inspect it. Parent and Roots return to broader scopes. The wheel displays a configurable set of five to eight nodes at once; rotation makes the remaining items reachable.'),
('Keep the presentation clear', 'Show Info opens the additional information panel, including Component Details and Bookmarks & History. Hide Info collapses it and returns the space to the wheel. The Bookmarks button stays available in the header even when the information panel is hidden. Its menu provides access to saved locations.'),
('Resize and read', 'Resize the window or its dock boundary to change the available viewing area. Header controls become compact and wrap as needed; the wheel scales to the available viewport. Node titles use bold text. Less frequently used controls remain accessible through Tools in compact layouts.'),
('Components and editing', 'Use Component Details or the native Unity Inspector to inspect the selected component. Adding, editing or removing a component changes the inspected object; review any confirmation prompt and use Unity Undo where applicable. Save your scene or asset when you intend to retain those changes. Play mode changes follow Unity play mode persistence rules.'),
('Settings', 'Quantum Inspector > Settings selects the settings asset. Wheel capacity, node size, radius and research panel options are configurable. Changes to the shared settings asset can affect other tool windows. The Show Info toggle maintains its own visibility preference.')]),
('Clustered Graph', [
('Selection and locks', 'Selecting a GameObject in Unity or an asset or folder in the Project window updates Quantum Inspector unless Lock Unity selection is enabled. Investigation Lock holds automatic arrangement and queues content changes; it does not block Project assets, folders or GameObjects from changing the view. Disable Lock Unity selection when the view appears to ignore the native Hierarchy or Project window.'),
('Explore relationships', 'Expand nodes incrementally to reveal related content. Overlays can expose hierarchy, serialized references, asset dependencies, events and prefab sources as applicable. Serialized relationships come from Unity serialized fields; arbitrary runtime code connections are not automatically discoverable. Unsupported assets may use a generic representation.'),
('Camera and arrangement', 'Drag empty graph space to pan, scroll to zoom, and press F while the graph has focus to fit the view. Drag nodes to reposition them. Shift-click to group a selection. Use Arrange for an explicit layout update and Apply Updates when changes have been queued during investigation.'),
('Presentation', 'Graph settings expose visual theme, motion, quality and auxiliary information controls. Overview and detail presentations vary with zoom. On large graphs, reveal only the relationships needed for the current investigation and reduce motion or quality if interaction becomes slow.'),
('Pins, history and bookmarks', 'A pinned node position preserves its placement during graph work. This is separate from locking Unity selection. Use Bookmarks for frequently visited locations and Back/Forward for recent navigation. References to deleted or unavailable objects cannot restore the original content.')]),
('Workspace and sample scene', [
('Workspace setup guide', 'Quantum Inspector > Layouts > Workspace Setup opens an interactive guide with shortcuts for the native Hierarchy, Project and Inspector windows. Follow its steps to place Hierarchy and Project tabs in the left dock, Quantum Inspector in the middle and Unity Inspector on the right. Opening the guide preserves existing docks. A newly opened Quantum Inspector also shows the setup guide; reopening an existing inspector preserves its window.'),
('A portable manual layout', 'Open Unity Hierarchy, Project and Inspector from Window > General. Drag Hierarchy and Project into the same left tab group, Quantum Inspector into the center and Inspector into the right dock. Save the arrangement using Unity Layouts > Save Layout. A saved personal layout is not a promise of automatic layout installation on another machine.'),
('Try the Living Laboratory', 'Save any unsaved work, then choose Quantum Inspector > Demo > Open Demo Scene. Select the named SHOWCASE objects to explore deep hierarchy, many siblings, multiple components, cross-referenced systems, large clusters, disabled objects and nested prefabs. Select the Transform-only showcase to see an empty component state.'),
('Motion and dense fixtures', 'The laboratory animates in Play mode. Additional graph fixtures include DenseComponentFixture.prefab with 42 attached components, of which 32 are DemoProbe instances. Open that prefab in Prefab Mode and select its root for a dense graph example. See Demo/Documentation/README.md and ClusterMotionGuide.md for the walkthrough.'),
('Regeneration caution', 'Generate or Regenerate Laboratory rebuilds generated demo content. It can replace manual edits to generated scene assets, materials, meshes and prefabs; read the confirmation before proceeding. Keep your own working copies outside the generated demo folders. The existing demo is sufficient for normal exploration.')]),
('Troubleshooting and package notes', [
('Compilation errors after import', 'Check the first Console error. Missing Unity.RenderPipelines.Universal or Core assemblies indicate that the demo dependency is missing or incompatible. Install a matching URP version, or omit the entire Demo folder when using only the core tool. Keep the tool Editor assembly and its source together.'),
('Missing or pink demo materials', 'Confirm that the active project uses URP and that its render pipeline asset is assigned. The demo does not convert Built-in or HDRP materials. Do not overwrite another project\'s ProjectSettings with this development project\'s settings.'),
('Unexpected navigation or stale content', 'Check Lock Unity selection, investigation lock and any pending Apply Updates action. Select a scene GameObject again. Use Roots to return to a known scope. If a bookmark points at deleted content, navigate to the replacement and create a new bookmark.'),
('Scope and known limitations', 'This is an Editor inspection tool, not a runtime in-game inspector. Graph discovery reflects supported Unity data and can become expensive for large dependency networks. The current build has been checked in its development project, not certified across every Unity version, render pipeline or operating system. Native dock arrangement is manual, assisted by the workspace setup guide.'),
('Contents, updates and licensing', 'Documentation includes this offline manual, a quick start, support details, changelog and third-party notices. Demo content is separated from the core Editor tool. Preserve .meta files when updating so Unity references remain stable. Back up custom settings before replacing package files. Asset Store purchases are governed by the license shown on the store listing; this manual does not add a separate license.'),
('Contact', 'gbear9551@gmail.com\nDeveloped by Gary Lougheed & Astra')])]

story=[]
for idx,(title,sections) in enumerate(pages):
    if idx: story.append(PageBreak())
    story.append(Paragraph(escape(title), styles['TitleQI']))
    for heading,body in sections:
        body=body.replace('Quantum Inspector >','Tools > Quantum Inspector >')
        story.append(KeepTogether([Paragraph(escape(heading),styles['SectionQI']), Paragraph(escape(body).replace('\n','<br/>'),styles['BodyQI'])]))

def footer(canvas,doc):
    w,h=doc.pagesize
    canvas.setStrokeColor(HexColor('#bed4dd'));canvas.line(44,42,w-44,42)
    canvas.setFont('Helvetica',8);canvas.setFillColor(HexColor('#466475'))
    canvas.drawString(44,28,'QUANTUM INSPECTOR  /  USER GUIDE  /  1.0.0')
    canvas.drawRightString(w-44,28,str(doc.page))

pdf=OUT/'QuantumInspector-UserGuide.pdf'
SimpleDocTemplate(str(pdf),pagesize=(595.28,841.89),leftMargin=44,rightMargin=44,topMargin=42,bottomMargin=58,title='Quantum Inspector User Guide',author='Gary Lougheed & Astra').build(story,onFirstPage=footer,onLaterPages=footer)
assert len(PdfReader(pdf).pages)==5, 'Unexpected pagination; inspect before release'
shutil.copy2(pdf,DEST/pdf.name)
renders=ROOT/'tmp/pdfs';renders.mkdir(parents=True,exist_ok=True)
doc=fitz.open(pdf)
for idx,page in enumerate(doc): page.get_pixmap(matrix=fitz.Matrix(1.2,1.2)).save(renders/f'guide-{idx+1}.png')
print(f'Created and rendered {len(doc)} pages: {pdf}')
