import readline from 'node:readline'
import path from 'node:path'
import os from 'node:os'
import mineflayer from 'mineflayer'

const activeBots = new Set()

function emit(level, message, data = undefined) {
  process.stdout.write(`${JSON.stringify({ type: 'log', level, message, data })}\n`)
}

function config(node, key, fallback = '') {
  return node?.config?.[key] ?? fallback
}

function template(input, context) {
  return String(input ?? '').replace(/\{\{\s*([^}]+?)\s*\}\}/g, (_, rawKey) => {
    const key = String(rawKey).trim()
    if (key in context.variables) return String(context.variables[key] ?? '')
    if (key in context) return String(context[key] ?? '')
    return ''
  })
}

function getPosition(bot) {
  const position = bot?.entity?.position
  return {
    x: position ? Number(position.x.toFixed(2)) : '',
    y: position ? Number(position.y.toFixed(2)) : '',
    z: position ? Number(position.z.toFixed(2)) : ''
  }
}

function createContext(bot, extra = {}) {
  return {
    bot,
    username: bot.username ?? '',
    health: bot.health ?? '',
    food: bot.food ?? '',
    ...getPosition(bot),
    sender: '',
    message: '',
    variables: {},
    ...extra
  }
}

function compare(left, operator, right) {
  switch (operator) {
    case 'equals': return left === right
    case 'notEquals': return left !== right
    case 'startsWith': return left.startsWith(right)
    case 'endsWith': return left.endsWith(right)
    case 'regex': {
      try { return new RegExp(right).test(left) } catch { return false }
    }
    case 'contains':
    default:
      return left.includes(right)
  }
}

function buildRuntime(workflow) {
  const nodes = new Map(workflow.nodes.map(node => [node.id, node]))
  const outgoing = new Map()
  const incoming = new Map()

  for (const edge of workflow.connections) {
    if (!outgoing.has(edge.sourceNode)) outgoing.set(edge.sourceNode, [])
    outgoing.get(edge.sourceNode).push(edge)
    if (!incoming.has(edge.targetNode)) incoming.set(edge.targetNode, [])
    incoming.get(edge.targetNode).push(edge)
  }

  function edgesFrom(nodeId, port) {
    return (outgoing.get(nodeId) ?? []).filter(edge => edge.sourcePort === port)
  }

  function inputSource(nodeId, port) {
    const edge = (incoming.get(nodeId) ?? []).find(item => item.targetPort === port)
    return edge ? nodes.get(edge.sourceNode) : undefined
  }

  async function runNext(sourceNodeId, sourcePort, context, visited = new Set()) {
    for (const edge of edgesFrom(sourceNodeId, sourcePort)) {
      await executeNode(edge.targetNode, context, new Set(visited))
    }
  }

  async function executeNode(nodeId, context, visited) {
    const node = nodes.get(nodeId)
    if (!node) return

    if (visited.has(nodeId)) {
      emit('error', `Cycle detected while executing node ${nodeId}. Explicit loop nodes are required for repeated execution.`)
      return
    }
    visited.add(nodeId)

    switch (node.type) {
      case 'logic.if': {
        const left = template(config(node, 'left', '{{message}}'), context)
        const right = template(config(node, 'right', ''), context)
        const operator = config(node, 'operator', 'contains')
        await runNext(node.id, compare(left, operator, right) ? 'true' : 'false', context, visited)
        return
      }

      case 'utility.wait': {
        const milliseconds = Math.max(0, Number.parseInt(config(node, 'milliseconds', '1000'), 10) || 0)
        await new Promise(resolve => setTimeout(resolve, milliseconds))
        await runNext(node.id, 'flow', context, visited)
        return
      }

      case 'utility.setVariable': {
        const name = config(node, 'name', 'value').trim()
        if (name) context.variables[name] = template(config(node, 'value', ''), context)
        await runNext(node.id, 'flow', context, visited)
        return
      }

      case 'utility.log': {
        emit('info', template(config(node, 'message', ''), context))
        await runNext(node.id, 'flow', context, visited)
        return
      }

      case 'action.sendChat': {
        const message = template(config(node, 'message', ''), context)
        if (message) context.bot.chat(message)
        await runNext(node.id, 'flow', context, visited)
        return
      }

      case 'action.sendCommand': {
        let command = template(config(node, 'command', ''), context).trim()
        if (command && !command.startsWith('/')) command = `/${command}`
        if (command) context.bot.chat(command)
        await runNext(node.id, 'flow', context, visited)
        return
      }

      case 'action.disconnect': {
        context.bot.quit(config(node, 'reason', 'Minenode workflow'))
        return
      }

      default:
        emit('warn', `Node ${node.type} is not executable in a flow context.`)
    }
  }

  async function attachJoin(joinNode) {
    const accountNode = inputSource(joinNode.id, 'bot')
    if (!accountNode || accountNode.type !== 'account.microsoft') {
      emit('error', `Join Server node ${joinNode.id} is missing a Microsoft Account input.`)
      return
    }

    const username = config(accountNode, 'username', '').trim()
    if (!username) {
      emit('error', `Microsoft Account node ${accountNode.id} has no account identifier.`)
      return
    }

    const host = config(joinNode, 'host', 'localhost').trim() || 'localhost'
    const port = Number.parseInt(config(joinNode, 'port', '25565'), 10) || 25565
    const versionSetting = config(joinNode, 'version', 'auto').trim()
    const profilesFolder = process.env.MINENODE_PROFILE_DIR || path.join(os.homedir(), '.minenode', 'profiles')

    emit('info', `Connecting ${username} to ${host}:${port}...`)

    const bot = mineflayer.createBot({
      host,
      port,
      username,
      auth: 'microsoft',
      profilesFolder,
      ...(versionSetting && versionSetting !== 'auto' ? { version: versionSetting } : {})
    })

    activeBots.add(bot)

    bot.once('spawn', async () => {
      emit('info', `Connected as ${bot.username}.`)
      const context = createContext(bot)
      for (const edge of edgesFrom(joinNode.id, 'bot')) {
        const target = nodes.get(edge.targetNode)
        if (target?.type === 'trigger.onSpawn')
          await runNext(target.id, 'flow', { ...context, variables: {} })
      }
    })

    bot.on('chat', async (sender, message) => {
      if (sender === bot.username) return
      const base = createContext(bot, { sender, message })
      for (const edge of edgesFrom(joinNode.id, 'bot')) {
        const target = nodes.get(edge.targetNode)
        if (target?.type === 'trigger.onChat')
          await runNext(target.id, 'flow', { ...base, variables: {} })
      }
    })

    bot.on('kicked', reason => emit('warn', `Kicked: ${String(reason)}`))
    bot.on('error', error => emit('error', error?.stack || error?.message || String(error)))
    bot.on('end', reason => {
      activeBots.delete(bot)
      emit('info', `Disconnected: ${String(reason ?? '')}`)
    })
  }

  return {
    async start() {
      const joins = workflow.nodes.filter(node => node.type === 'connection.join')
      if (joins.length === 0) {
        emit('warn', 'Workflow has no Join Server nodes.')
        return
      }
      await Promise.all(joins.map(attachJoin))
    }
  }
}

async function stopAll() {
  for (const bot of [...activeBots]) {
    try { bot.quit('Minenode stopped') } catch {}
  }
  activeBots.clear()
}

const input = readline.createInterface({ input: process.stdin, crlfDelay: Infinity })

input.on('line', async line => {
  if (!line.trim()) return

  try {
    const message = JSON.parse(line)
    if (message.type === 'run') {
      await stopAll()
      emit('info', `Starting workflow: ${message.workflow?.name ?? 'Untitled'}`)
      await buildRuntime(message.workflow).start()
    } else if (message.type === 'stop') {
      await stopAll()
      emit('info', 'Workflow stopped.')
    }
  } catch (error) {
    emit('error', error?.stack || error?.message || String(error))
  }
})

process.on('SIGTERM', async () => {
  await stopAll()
  process.exit(0)
})

process.on('SIGINT', async () => {
  await stopAll()
  process.exit(0)
})

emit('info', 'Minenode runtime ready.')
