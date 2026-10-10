export interface CurrentUser {
  subject: string
  name: string | null
  groups: string[]
  isAdministrator: boolean
  permissionExpiresAtUtc: string
}

export interface PortalItem {
  id: string
  scope: 'Personal' | 'Public'
  name: string
  description: string | null
  url: string
  iconKind: string
  iconValue: string
  color: string
  sortOrder: number
  isEnabled: boolean
  version: number
}

export interface AgentHealth {
  available: boolean
  dockerAvailable: boolean
  systemdAvailable: boolean
  status: string
  observedAtUtc: string
}

export interface HostInfo {
  hostName: string
  distribution: string
  kernelVersion: string
  architecture: string
  bootTimeUtc: string
  uptime: string
  logicalProcessorCount: number
  totalMemoryBytes: number
  addresses: Array<{ interfaceName: string; address: string }>
}

export interface JournalEntry {
  occurredAtUtc: string
  unit: string
  priority: string
  process: string
  processId: number
  message: string
}

export interface ManagedResource {
  id: string
  name: string
  type: string
  state: string
  version: string
  isProtected: boolean
  attributes: Record<string, string>
}

export interface MetricValue {
  kind: string
  deviceId: string
  value: number
  unit: string
  quality: string
  sampledAtUtc: string
}

export interface MetricSeriesSnapshot {
  kind: string
  deviceId: string
  unit: string
  points: Array<{ sampledAtUtc: string; value: number | null; quality: string }>
}

export interface CatalogApplication {
  id: string
  name: string
  description: string
  image: string
  version: string
  iconUrl: string | null
  installed: boolean
  state: string | null
}

export interface ApplicationImageMetadata {
  image: string
  imageId: string
  environment: string[]
  exposedPorts: string[]
  volumes: string[]
  entrypoint: string[]
  command: string[]
  labels: Record<string, string>
}

export interface OverviewResponse {
  scope: 'Public' | 'Administrator'
  platformStatus: string
  sampledAtUtc: string
  portals: PortalItem[]
  agent: AgentHealth | null
  host: HostInfo | null
  resources: ManagedResource[] | null
  metrics: MetricValue[] | null
  applications: CatalogApplication[] | null
  catalogIsStale: boolean
}

export interface Job {
  id: string
  jobType: string
  state: string
  phase: string
  progressPercent: number | null
  errorCode: string | null
  createdAtUtc: string
  completedAtUtc: string | null
  resultJson: string | null
}

export interface JobEvent {
  sequence: number
  state: string
  phase: string
  progressPercent: number | null
  messageCode: string
  occurredAtUtc: string
}

export interface ScheduledTask {
  id: string
  taskType: string
  name: string
  scheduleKind: string
  scheduleExpression: string
  timezone: string
  parametersJson: string
  concurrencyPolicy: string
  timeoutSeconds: number
  isEnabled: boolean
  nextRunAtUtc: string | null
  version: number
}

export interface AlertRule {
  id: string
  ruleType: string
  resourceSelectorJson: string
  thresholdJson: string
  evaluationWindowSeconds: number
  severity: 'Info' | 'Warning' | 'Critical'
  isEnabled: boolean
  version: number
}

export interface AlertEvent {
  id: string
  ruleId: string
  resourceId: string | null
  state: string
  severity: 'Info' | 'Warning' | 'Critical'
  occurrenceCount: number
  firstOccurredAtUtc: string
  lastOccurredAtUtc: string
  recoveredAtUtc: string | null
  acknowledgedBySubject: string | null
  acknowledgedAtUtc: string | null
  silencedUntilUtc: string | null
  summaryCode: string
}

export interface PlatformSetting {
  key: string
  valueJson: string
  version: number
  updatedAtUtc: string
}

export interface BackupPolicy {
  id: string
  instanceResourceId: string
  isEnabled: boolean
  scheduleExpression: string
  timezone: string
  retentionCount: number
  retentionDays: number
  targetDirectoryId: string
  compression: string
  verifyAfterBackup: boolean
  capacityWarningPercent: number
  capacityCriticalPercent: number
  version: number
}
