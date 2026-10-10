import { apiRequest } from '@/services/apiClient'
import type { MetricSeriesSnapshot, OverviewResponse, PortalItem } from '@/services/contracts'

export type ResourceKey = 'agents' | 'websites' | 'databases' | 'containers'
export type ContainerState = 'available' | 'running' | 'stopped' | 'restarting'

export interface ResourceSummary {
  key: ResourceKey
  label: string
  value: number
  unit: string
  detail: string
}

export interface UsageMetric {
  key: 'cpu' | 'memory' | 'disk'
  label: string
  percentage: number
  value: string
  detail: string
}

export interface TelemetryStat {
  label: string
  value: string
  tone: 'primary' | 'success' | 'warning' | 'info'
}

export interface TelemetrySeries {
  name: string
  color: string
  values: number[]
}

export interface TelemetryFilter {
  value: string
  label: string
  stats: TelemetryStat[]
  series: TelemetrySeries[]
}

export interface TelemetryDataset {
  unit: string
  labels: string[]
  filters: TelemetryFilter[]
}

export interface SystemInformation {
  label: string
  value: string
  hint?: string
}

export interface RecommendedApplication {
  id: string
  name: string
  version: string
  description: string
  category: string
  icon: 'gateway' | 'database' | 'cache' | 'ai' | 'runtime' | 'developer'
  state: ContainerState
  installedVersion?: string
}

export interface WorkstationOverviewSnapshot {
  scope: 'Public' | 'Administrator'
  sampledAtUtc: string
  platformStatus: string
  portals: PortalItem[]
  resources: ResourceSummary[]
  healthMessage: string
  healthDetail: string
  usage: UsageMetric[]
  network: TelemetryDataset
  diskIo: TelemetryDataset
  system: SystemInformation[]
  applications: RecommendedApplication[]
}

const labels = ['20:31', '20:32', '20:33', '20:34', '20:35', '20:36', '20:37', '20:38']

export const demoWorkstationOverview: WorkstationOverviewSnapshot = {
  scope: 'Administrator',
  sampledAtUtc: new Date().toISOString(),
  platformStatus: 'Preview',
  portals: [],
  resources: [
    { key: 'agents', label: '智能体', value: 4, unit: '个', detail: '2 个正在执行任务' },
    { key: 'websites', label: '网站', value: 6, unit: '个', detail: '6 个站点可正常访问' },
    { key: 'databases', label: '数据库', value: 5, unit: '个', detail: '全部限制在容器网络' },
    { key: 'containers', label: '容器', value: 12, unit: '个', detail: '11 运行 · 1 已停止' },
  ],
  healthMessage: '主机资源正常',
  healthDetail: 'CPU、内存与磁盘均处于正常范围',
  usage: [
    { key: 'cpu', label: 'CPU 使用率', percentage: 18.6, value: '18.6%', detail: '5 秒采样窗口' },
    {
      key: 'memory',
      label: '内存使用率',
      percentage: 42.8,
      value: '54.8 / 128 GB',
      detail: '可用 73.2 GB',
    },
    {
      key: 'disk',
      label: '硬盘使用率',
      percentage: 61.3,
      value: '2.23 / 3.64 TB',
      detail: 'NVMe 数据卷',
    },
  ],
  network: {
    unit: 'MB/s',
    labels,
    filters: [
      {
        value: 'all',
        label: '全部网卡',
        stats: [
          { label: '当前上行', value: '2.8 MB/s', tone: 'primary' },
          { label: '当前下行', value: '12.4 MB/s', tone: 'success' },
          { label: '累计发送', value: '1.16 TB', tone: 'info' },
          { label: '累计接收', value: '3.84 TB', tone: 'warning' },
        ],
        series: [
          {
            name: '下行',
            color: '#409eff',
            values: [8.2, 12.6, 9.4, 18.1, 13.8, 22.4, 17.2, 12.4],
          },
          { name: '上行', color: '#67c23a', values: [2.1, 3.4, 2.8, 5.2, 4.1, 6.4, 4.7, 2.8] },
        ],
      },
      {
        value: 'internal',
        label: '内网 · 192.168.22.19',
        stats: [
          { label: '当前上行', value: '2.4 MB/s', tone: 'primary' },
          { label: '当前下行', value: '9.7 MB/s', tone: 'success' },
          { label: '累计发送', value: '846 GB', tone: 'info' },
          { label: '累计接收', value: '2.91 TB', tone: 'warning' },
        ],
        series: [
          { name: '下行', color: '#409eff', values: [6.4, 10.1, 8.7, 14.9, 11.8, 18.2, 14.1, 9.7] },
          { name: '上行', color: '#67c23a', values: [1.8, 2.8, 2.2, 4.4, 3.6, 5.2, 3.9, 2.4] },
        ],
      },
      {
        value: 'external',
        label: '外网 · 192.168.100.13',
        stats: [
          { label: '当前上行', value: '0.4 MB/s', tone: 'primary' },
          { label: '当前下行', value: '2.7 MB/s', tone: 'success' },
          { label: '累计发送', value: '342 GB', tone: 'info' },
          { label: '累计接收', value: '953 GB', tone: 'warning' },
        ],
        series: [
          { name: '下行', color: '#409eff', values: [1.8, 2.5, 0.7, 3.2, 2.0, 4.2, 3.1, 2.7] },
          { name: '上行', color: '#67c23a', values: [0.3, 0.6, 0.6, 0.8, 0.5, 1.2, 0.8, 0.4] },
        ],
      },
    ],
  },
  diskIo: {
    unit: 'MB/s',
    labels,
    filters: [
      {
        value: 'all',
        label: '全部磁盘',
        stats: [
          { label: '读取', value: '86.4 MB/s', tone: 'success' },
          { label: '写入', value: '24.8 MB/s', tone: 'warning' },
          { label: '读写次数', value: '1,284 /s', tone: 'primary' },
          { label: '平均延迟', value: '1.8 ms', tone: 'info' },
        ],
        series: [
          { name: '读取', color: '#409eff', values: [42, 65, 48, 112, 76, 138, 104, 86.4] },
          { name: '写入', color: '#e6a23c', values: [18, 22, 14, 31, 19, 46, 37, 24.8] },
        ],
      },
      {
        value: 'nvme',
        label: 'NVMe · 数据盘',
        stats: [
          { label: '读取', value: '82.1 MB/s', tone: 'success' },
          { label: '写入', value: '21.6 MB/s', tone: 'warning' },
          { label: '读写次数', value: '1,148 /s', tone: 'primary' },
          { label: '平均延迟', value: '0.9 ms', tone: 'info' },
        ],
        series: [
          { name: '读取', color: '#409eff', values: [39, 62, 44, 107, 72, 131, 98, 82.1] },
          { name: '写入', color: '#e6a23c', values: [15, 20, 11, 27, 16, 41, 33, 21.6] },
        ],
      },
      {
        value: 'hdd',
        label: 'HDD · 备份盘',
        stats: [
          { label: '读取', value: '4.3 MB/s', tone: 'success' },
          { label: '写入', value: '3.2 MB/s', tone: 'warning' },
          { label: '读写次数', value: '136 /s', tone: 'primary' },
          { label: '平均延迟', value: '8.7 ms', tone: 'info' },
        ],
        series: [
          { name: '读取', color: '#409eff', values: [3.2, 2.6, 4.1, 5.3, 3.8, 6.6, 5.7, 4.3] },
          { name: '写入', color: '#e6a23c', values: [2.1, 2.4, 2.8, 3.9, 2.7, 5.2, 4.0, 3.2] },
        ],
      },
    ],
  },
  system: [
    { label: '主机名称', value: 'Precision-7920-Tower' },
    { label: '发行版本', value: 'Ubuntu 24.04 LTS' },
    { label: '内核版本', value: '6.8.0-generic' },
    { label: '系统类型', value: 'Linux · x86_64' },
    { label: '内网地址', value: '192.168.22.19', hint: '容器访问' },
    { label: '外网地址', value: '192.168.100.13', hint: '下载与本机访问' },
    { label: '启动时间', value: '2026-10-06 08:42' },
    { label: '运行时间', value: '3 天 12 小时 18 分' },
  ],
  applications: [
    {
      id: 'openresty',
      name: 'OpenResty',
      version: '1.27.1.2',
      description: '高性能 Web 平台与反向代理',
      category: 'Web 网关',
      icon: 'gateway',
      state: 'available',
    },
    {
      id: 'mysql',
      name: 'MySQL',
      version: '8.4',
      description: '关系型数据库服务',
      category: '数据库',
      icon: 'database',
      state: 'running',
      installedVersion: '8.4.6',
    },
    {
      id: 'redis',
      name: 'Redis',
      version: '7.4',
      description: '内存数据结构与缓存服务',
      category: '缓存',
      icon: 'cache',
      state: 'available',
    },
    {
      id: 'onepanel-ai',
      name: '1Panel AI 网关',
      version: 'latest',
      description: '统一接入与治理企业 AI 服务',
      category: 'AI 服务',
      icon: 'ai',
      state: 'available',
    },
    {
      id: 'laya-server',
      name: 'Laya Server',
      version: 'latest',
      description: 'Laya 游戏引擎 API 与控制台',
      category: '运行环境',
      icon: 'runtime',
      state: 'available',
    },
    {
      id: 'deepseek-harness',
      name: 'DeepSeek Harness',
      version: 'latest',
      description: 'DeepSeek 开源智能体开发环境',
      category: '开发工具',
      icon: 'developer',
      state: 'available',
    },
  ],
}

export async function fetchWorkstationOverview(): Promise<WorkstationOverviewSnapshot> {
  if (import.meta.env.DEV) return structuredClone(demoWorkstationOverview)

  const response = await apiRequest<OverviewResponse>('overview')
  if (response.scope === 'Public') {
    return {
      ...structuredClone(demoWorkstationOverview),
      scope: 'Public',
      sampledAtUtc: response.sampledAtUtc,
      platformStatus: response.platformStatus,
      portals: response.portals,
      resources: [], usage: [], system: [], applications: [],
      healthMessage: '平台在线',
      healthDetail: '当前账号可访问个人门户与已发布入口',
    }
  }

  const resources = response.resources ?? []
  const history = await apiRequest<MetricSeriesSnapshot[]>('metrics?kinds=network.receive&kinds=network.send&kinds=network.receive.total&kinds=network.send.total&kinds=disk.read&kinds=disk.write&kinds=disk.utilization&take=60')
  const metric = (kind: string) => response.metrics?.find((item) => item.kind === kind)?.value ?? 0
  const cpu = metric('cpu.utilization')
  const memory = metric('memory.utilization')
  const disk = metric('disk.utilization')
  const containers = resources.filter((item) => item.type === 'Container')
  const running = containers.filter((item) => item.state.toLowerCase() === 'running').length
  const uptime = response.host?.uptime || '未知'
  const bootTime = response.host?.bootTimeUtc ? new Date(response.host.bootTimeUtc).toLocaleString() : '未知'
  const applications = (response.applications ?? []).slice(0, 6).map((app, index) => ({
    id: app.id,
    name: app.name,
    version: app.version,
    description: app.description,
    category: '容器应用',
    icon: (['gateway', 'database', 'cache', 'ai', 'runtime', 'developer'] as const)[index % 6]!,
    state: (app.installed ? (app.state?.toLowerCase() === 'stopped' ? 'stopped' : 'running') : 'available') as ContainerState,
    installedVersion: app.installed ? app.version : undefined,
  }))
  const makeTelemetry = (mode: 'network' | 'disk'): TelemetryDataset => {
    const firstKind = mode === 'network' ? 'network.receive' : 'disk.read'
    const secondKind = mode === 'network' ? 'network.send' : 'disk.write'
    const devices = [...new Set(history.filter((series) => series.kind === firstKind || series.kind === secondKind).map((series) => series.deviceId))]
    const labels = history.find((series) => series.kind === firstKind)?.points.map((point) => new Date(point.sampledAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })) ?? []
    const filters = devices.map((device) => {
      const first = history.find((series) => series.kind === firstKind && series.deviceId === device)?.points ?? []
      const second = history.find((series) => series.kind === secondKind && series.deviceId === device)?.points ?? []
      const firstValue = first[first.length - 1]?.value ?? 0
      const secondValue = second[second.length - 1]?.value ?? 0
      return {
        value: device,
        label: device,
        stats: mode === 'network'
          ? [
              { label: '当前下行', value: formatRate(firstValue), tone: 'success' as const },
              { label: '当前上行', value: formatRate(secondValue), tone: 'primary' as const },
              { label: '采样点', value: String(Math.max(first.length, second.length)), tone: 'info' as const },
            ]
          : [
              { label: '读取', value: formatRate(firstValue), tone: 'success' as const },
              { label: '写入', value: formatRate(secondValue), tone: 'warning' as const },
              { label: '采样点', value: String(Math.max(first.length, second.length)), tone: 'info' as const },
            ],
        series: [
          { name: mode === 'network' ? '下行' : '读取', color: '#409eff', values: first.map((point) => (point.value ?? 0) / 1024 / 1024) },
          { name: mode === 'network' ? '上行' : '写入', color: mode === 'network' ? '#67c23a' : '#e6a23c', values: second.map((point) => (point.value ?? 0) / 1024 / 1024) },
        ],
      }
    })
    return {
      unit: 'MB/s',
      labels,
      filters: filters.length > 0 ? filters : [{ value: 'none', label: '暂无设备采样', stats: [], series: [] }],
    }
  }

  return {
    ...structuredClone(demoWorkstationOverview),
    scope: response.scope,
    sampledAtUtc: response.sampledAtUtc,
    platformStatus: response.platformStatus,
    portals: response.portals,
    resources: [
      { key: 'agents', label: '智能体', value: response.agent?.available ? 1 : 0, unit: '个', detail: response.agent?.status || '不可用' },
      { key: 'websites', label: '网站', value: response.portals.length, unit: '个', detail: '可访问门户入口' },
      { key: 'databases', label: '数据库', value: containers.filter((item) => item.name.includes('database-platform')).length, unit: '个', detail: '基础依赖容器' },
      { key: 'containers', label: '容器', value: containers.length, unit: '个', detail: `${running} 运行 · ${containers.length - running} 未运行` },
    ],
    healthMessage: response.agent?.available && response.agent.dockerAvailable ? '主机资源正常' : '管理服务降级',
    healthDetail: `Agent ${response.agent?.status || '不可用'} · 采样 ${new Date(response.sampledAtUtc).toLocaleTimeString()}`,
    usage: [
      { key: 'cpu', label: 'CPU 使用率', percentage: cpu, value: `${cpu.toFixed(1)}%`, detail: '6 秒采样' },
      { key: 'memory', label: '内存使用率', percentage: memory, value: `${memory.toFixed(1)}%`, detail: response.host ? `${(response.host.totalMemoryBytes / 1024 ** 3).toFixed(1)} GB 总内存` : '无主机数据' },
      { key: 'disk', label: '硬盘使用率', percentage: disk, value: `${disk.toFixed(1)}%`, detail: '首个已挂载文件系统' },
    ],
    system: [
      { label: '主机名称', value: response.host?.hostName || '未知' },
      { label: '发行版本', value: response.host?.distribution || '未知' },
      { label: '内核版本', value: response.host?.kernelVersion || '未知' },
      { label: '系统类型', value: response.host?.architecture || '未知' },
      { label: '内网地址', value: '192.168.22.19', hint: '容器访问' },
      { label: '外网地址', value: '192.168.100.13', hint: '下载与局域网访问' },
      { label: '启动时间', value: bootTime },
      { label: '运行时间', value: uptime },
    ],
    applications,
    network: makeTelemetry('network'),
    diskIo: makeTelemetry('disk'),
  }
}

function formatRate(bytesPerSecond: number) {
  return `${(bytesPerSecond / 1024 / 1024).toFixed(2)} MB/s`
}
