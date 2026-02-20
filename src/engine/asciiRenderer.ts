export const RenderLayer = {
  Base: 'Base',
  Entities: 'Entities',
  Particles: 'Particles',
  UI: 'UI',
} as const

export type RenderLayer = (typeof RenderLayer)[keyof typeof RenderLayer]

export interface GridPoint {
  x: number
  y: number
}

export interface AsciiCell {
  char: string
  foreground: string
  background: string
}

const layerOrder = [RenderLayer.Base, RenderLayer.Entities, RenderLayer.Particles, RenderLayer.UI]

export class AsciiRenderer {
  private readonly layers = new Map<RenderLayer, Map<string, AsciiCell>>()
  public readonly width: number
  public readonly height: number

  constructor(width: number, height: number) {
    this.width = width
    this.height = height
    layerOrder.forEach((layer) => this.layers.set(layer, new Map<string, AsciiCell>()))
  }

  setCell(layer: RenderLayer, x: number, y: number, cell: AsciiCell): void {
    if (!this.isInside(x, y)) return
    this.layers.get(layer)?.set(this.key(x, y), cell)
  }

  clearLayer(layer: RenderLayer): void {
    this.layers.get(layer)?.clear()
  }

  computeFov(origin: GridPoint, radius: number, isOpaque: (x: number, y: number) => boolean): Set<string> {
    const visible = new Set<string>()
    for (let y = origin.y - radius; y <= origin.y + radius; y += 1) {
      for (let x = origin.x - radius; x <= origin.x + radius; x += 1) {
        if (!this.isInside(x, y)) continue
        if (Math.hypot(x - origin.x, y - origin.y) > radius) continue
        if (this.hasLineOfSight(origin, { x, y }, isOpaque)) visible.add(this.key(x, y))
      }
    }
    return visible
  }

  toTextGrid(visible?: Set<string>): string {
    const rows: string[] = []
    for (let y = 0; y < this.height; y += 1) {
      let row = ''
      for (let x = 0; x < this.width; x += 1) {
        const key = this.key(x, y)
        if (visible && !visible.has(key)) {
          row += ' '
          continue
        }
        row += this.getTopCell(key)?.char ?? ' '
      }
      rows.push(row)
    }
    return rows.join('\n')
  }

  private getTopCell(key: string): AsciiCell | undefined {
    for (let index = layerOrder.length - 1; index >= 0; index -= 1) {
      const cell = this.layers.get(layerOrder[index])?.get(key)
      if (cell) return cell
    }
  }

  private hasLineOfSight(from: GridPoint, to: GridPoint, isOpaque: (x: number, y: number) => boolean): boolean {
    let x = from.x
    let y = from.y
    const dx = Math.abs(to.x - from.x)
    const dy = Math.abs(to.y - from.y)
    const sx = from.x < to.x ? 1 : -1
    const sy = from.y < to.y ? 1 : -1
    let err = dx - dy

    while (x !== to.x || y !== to.y) {
      if (!(x === from.x && y === from.y) && isOpaque(x, y)) return false
      const e2 = err * 2
      if (e2 > -dy) {
        err -= dy
        x += sx
      }
      if (e2 < dx) {
        err += dx
        y += sy
      }
    }
    return !isOpaque(to.x, to.y)
  }

  private isInside(x: number, y: number): boolean {
    return x >= 0 && y >= 0 && x < this.width && y < this.height
  }

  private key(x: number, y: number): string {
    return `${x},${y}`
  }
}
