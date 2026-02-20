type EventType = 'OnPlayerStep' | 'OnDeath' | 'OnTurnStart'
type ActionType = 'SpawnEntity' | 'ModifyHealth' | 'ShowMessage'

interface ScriptNodeBase {
  id: string
  type: EventType | ActionType
}

interface EventNode extends ScriptNodeBase {
  kind: 'Event'
  type: EventType
}

interface ActionNode extends ScriptNodeBase {
  kind: 'Action'
  type: ActionType
  payload?: Record<string, unknown>
}

interface ScriptEdge {
  from: string
  to: string
}

export interface ScriptRuntime {
  spawnEntity?: (payload?: Record<string, unknown>) => void
  modifyHealth?: (payload?: Record<string, unknown>) => void
  showMessage?: (payload?: Record<string, unknown>) => void
}

export class VisualScriptGraph {
  private counter = 0
  private readonly nodes = new Map<string, EventNode | ActionNode>()
  private readonly edges: ScriptEdge[] = []

  addEventNode(type: EventType): EventNode {
    const node: EventNode = { id: this.nextId(), kind: 'Event', type }
    this.nodes.set(node.id, node)
    return node
  }

  addActionNode(type: ActionType, payload?: Record<string, unknown>): ActionNode {
    const node: ActionNode = { id: this.nextId(), kind: 'Action', type, payload }
    this.nodes.set(node.id, node)
    return node
  }

  connect(fromId: string, toId: string): void {
    const from = this.nodes.get(fromId)
    const to = this.nodes.get(toId)
    if (!from || !to || from.kind !== 'Event' || to.kind !== 'Action') {
      throw new Error('Only Event -> Action connections are allowed.')
    }
    this.edges.push({ from: fromId, to: toId })
  }

  trigger(eventType: EventType, runtime: ScriptRuntime): void {
    const events = [...this.nodes.values()].filter(
      (node): node is EventNode => node.kind === 'Event' && node.type === eventType,
    )
    events.forEach((event) => {
      const actions = this.edges
        .filter((edge) => edge.from === event.id)
        .map((edge) => this.nodes.get(edge.to))
        .filter((node): node is ActionNode => !!node && node.kind === 'Action')
      actions.forEach((action) => this.execute(action, runtime))
    })
  }

  describe(): string {
    const eventCount = [...this.nodes.values()].filter((n) => n.kind === 'Event').length
    const actionCount = [...this.nodes.values()].filter((n) => n.kind === 'Action').length
    return `${eventCount} event node(s), ${actionCount} action node(s), ${this.edges.length} link(s).`
  }

  private execute(action: ActionNode, runtime: ScriptRuntime): void {
    if (action.type === 'SpawnEntity') runtime.spawnEntity?.(action.payload)
    if (action.type === 'ModifyHealth') runtime.modifyHealth?.(action.payload)
    if (action.type === 'ShowMessage') runtime.showMessage?.(action.payload)
  }

  private nextId(): string {
    this.counter += 1
    return `node-${this.counter}`
  }
}
