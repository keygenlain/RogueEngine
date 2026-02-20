import { useMemo, type CSSProperties } from 'react'

export interface MenuWidget {
  id: string
  kind: 'panel' | 'label' | 'button'
  text: string
  x: number
  y: number
  width: number
  height: number
}

interface DragDropMenuBuilderProps {
  gridWidth: number
  gridHeight: number
  cellSize: number
  widgets: MenuWidget[]
  onChange: (widgets: MenuWidget[]) => void
}

const palette: Array<Pick<MenuWidget, 'kind' | 'text' | 'width' | 'height'>> = [
  { kind: 'panel', text: 'Panel', width: 6, height: 3 },
  { kind: 'label', text: 'Label', width: 6, height: 1 },
  { kind: 'button', text: 'Button', width: 6, height: 1 },
]

export function DragDropMenuBuilder({ gridWidth, gridHeight, cellSize, widgets, onChange }: DragDropMenuBuilderProps) {
  const style = useMemo(
    () =>
      ({
        width: gridWidth * cellSize,
        height: gridHeight * cellSize,
        backgroundSize: `${cellSize}px ${cellSize}px`,
      }) satisfies CSSProperties,
    [cellSize, gridHeight, gridWidth],
  )

  return (
    <div className="menu-builder">
      <aside className="palette">
        {palette.map((item) => (
          <button
            key={item.kind}
            className="palette-item"
            draggable
            onDragStart={(event) => event.dataTransfer.setData('application/menu-widget', JSON.stringify(item))}
          >
            {item.kind}
          </button>
        ))}
      </aside>
      <div
        className="grid-canvas"
        style={style}
        onDragOver={(event) => event.preventDefault()}
        onDrop={(event) => {
          event.preventDefault()
          const raw = event.dataTransfer.getData('application/menu-widget')
          if (!raw) return
          const widget = JSON.parse(raw) as Pick<MenuWidget, 'kind' | 'text' | 'width' | 'height'>
          const rect = event.currentTarget.getBoundingClientRect()
          const x = Math.max(0, Math.min(gridWidth - widget.width, Math.floor((event.clientX - rect.left) / cellSize)))
          const y = Math.max(0, Math.min(gridHeight - widget.height, Math.floor((event.clientY - rect.top) / cellSize)))
          const nextWidget: MenuWidget = {
            ...widget,
            id: `${widget.kind}-${Date.now()}`,
            x,
            y,
          }
          onChange([...widgets, nextWidget])
        }}
      >
        {widgets.map((widget) => (
          <div
            key={widget.id}
            className={`widget widget-${widget.kind}`}
            style={{
              left: widget.x * cellSize,
              top: widget.y * cellSize,
              width: widget.width * cellSize,
              height: widget.height * cellSize,
            }}
          >
            {widget.text}
          </div>
        ))}
      </div>
    </div>
  )
}
