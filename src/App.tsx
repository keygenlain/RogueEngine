import { useMemo, useState } from 'react'
import './App.css'
import { AsciiRenderer, RenderLayer } from './engine/asciiRenderer'
import { VisualScriptGraph } from './engine/visualScripting'
import { DragDropMenuBuilder, type MenuWidget } from './engine/menuBuilder'

function App() {
  const [hudWidgets, setHudWidgets] = useState<MenuWidget[]>([
    { id: 'player-hp', kind: 'label', text: 'HP: 10/10', x: 0, y: 0, width: 8, height: 1 },
  ])

  const renderedPreview = useMemo(() => {
    const renderer = new AsciiRenderer(24, 8)
    renderer.setCell(RenderLayer.Base, 2, 2, { char: '.', foreground: '#8f9ba8', background: '#11161d' })
    renderer.setCell(RenderLayer.Base, 3, 2, { char: '.', foreground: '#8f9ba8', background: '#11161d' })
    renderer.setCell(RenderLayer.Entities, 4, 2, { char: '@', foreground: '#9be564', background: '#11161d' })
    renderer.setCell(RenderLayer.UI, 0, 0, { char: 'H', foreground: '#ffd166', background: '#11161d' })
    renderer.setCell(RenderLayer.UI, 1, 0, { char: 'P', foreground: '#ffd166', background: '#11161d' })
    const visible = renderer.computeFov({ x: 4, y: 2 }, 5, (x, y) => x === 10 && y < 5)
    return renderer.toTextGrid(visible)
  }, [])

  const scriptSummary = useMemo(() => {
    const graph = new VisualScriptGraph()
    const event = graph.addEventNode('OnPlayerStep')
    const action = graph.addActionNode('ShowMessage', { text: 'You moved.' })
    graph.connect(event.id, action.id)
    return graph.describe()
  }, [])

  return (
    <main className="app">
      <h1>RogueEngine</h1>
      <p className="subtitle">TypeScript + React engine toolkit for ASCII roguelikes with HTML5 export.</p>
      <section className="panel">
        <h2>ASCII Renderer</h2>
        <p>Unicode grid, layered draw order (Base/Entities/Particles/UI), and line-of-sight FOV output.</p>
        <pre className="ascii-preview">{renderedPreview}</pre>
      </section>
      <section className="panel">
        <h2>Visual Scripting</h2>
        <p>{scriptSummary}</p>
      </section>
      <section className="panel">
        <h2>Drag-and-Drop Menu Builder</h2>
        <DragDropMenuBuilder gridWidth={24} gridHeight={8} cellSize={24} widgets={hudWidgets} onChange={setHudWidgets} />
      </section>
    </main>
  )
}

export default App
