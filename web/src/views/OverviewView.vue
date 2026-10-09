<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  ArrowRight,
  Box,
  CircleCheckFilled,
  Coin,
  Cpu,
  DataAnalysis,
  MagicStick,
  Monitor,
  Platform,
  Promotion,
  Refresh,
  RefreshRight,
  SwitchButton,
} from '@element-plus/icons-vue'
import TelemetryChart from '@/components/overview/TelemetryChart.vue'
import {
  demoWorkstationOverview,
  fetchWorkstationOverview,
  type RecommendedApplication,
  type ResourceKey,
} from '@/services/workstationOverview'

type MonitorMode = 'network' | 'disk'

const snapshot = ref(structuredClone(demoWorkstationOverview))
const monitorMode = ref<MonitorMode>('network')
const networkFilter = ref('all')
const diskFilter = ref('all')
const refreshing = ref(false)
const updatedAt = ref('刚刚')

const resourceIcons = {
  agents: MagicStick,
  websites: Monitor,
  databases: Coin,
  containers: Box,
} satisfies Record<ResourceKey, object>

const applicationIcons = {
  gateway: Promotion,
  database: DataAnalysis,
  cache: Coin,
  ai: MagicStick,
  runtime: Platform,
  developer: Cpu,
} as const

const monitorOptions = [
  { label: '流量监控', value: 'network' },
  { label: '磁盘 IO', value: 'disk' },
]

const activeTelemetry = computed(() =>
  monitorMode.value === 'network' ? snapshot.value.network : snapshot.value.diskIo,
)

const selectedTelemetryFilter = computed({
  get: () => (monitorMode.value === 'network' ? networkFilter.value : diskFilter.value),
  set: (value: string) => {
    if (monitorMode.value === 'network') networkFilter.value = value
    else diskFilter.value = value
  },
})

const activeFilter = computed(
  () =>
    activeTelemetry.value.filters.find((item) => item.value === selectedTelemetryFilter.value) ??
    activeTelemetry.value.filters[0]!,
)

function gaugeColor(percentage: number) {
  if (percentage >= 85) return 'var(--el-color-danger)'
  if (percentage >= 70) return 'var(--el-color-warning)'
  return 'var(--el-color-primary)'
}

async function refreshOverview(notify = true) {
  refreshing.value = true
  try {
    snapshot.value = await fetchWorkstationOverview()
    updatedAt.value = new Intl.DateTimeFormat('zh-CN', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    }).format(new Date())
    if (notify) ElMessage.success('演示数据已刷新')
  } finally {
    refreshing.value = false
  }
}

function showDeferredAction(action: 'details' | 'install', app: RecommendedApplication) {
  const actionLabel = action === 'details' ? '更多详情' : '安装'
  ElMessage.info(`${app.name}：${actionLabel}将在后端服务接入后开放`)
}

async function confirmContainerAction(app: RecommendedApplication, action: 'stop' | 'restart') {
  const actionLabel = action === 'stop' ? '关闭' : '重启'
  const confirmText = action === 'stop' ? '确认关闭' : '确认重启'

  try {
    await ElMessageBox.confirm(
      `将${actionLabel} ${app.name} ${app.installedVersion ?? ''}。当前为前端预览，仅更新页面状态。`,
      `${actionLabel}容器确认`,
      {
        type: 'warning',
        confirmButtonText: confirmText,
        cancelButtonText: '取消',
        distinguishCancelAndClose: true,
      },
    )
  } catch {
    return
  }

  app.state = action === 'stop' ? 'stopped' : 'running'
  ElMessage.success(`${app.name} 已在预览中标记为${action === 'stop' ? '已停止' : '运行中'}`)
}

onMounted(() => {
  void refreshOverview(false)
})
</script>

<template>
  <div class="overview-page">
    <section id="overview" class="page-heading overview-heading">
      <div>
        <div class="page-heading__meta">
          <el-tag size="small" type="primary" effect="light" round>WORKSTATION</el-tag>
          <span>Precision-7920-Tower</span>
          <el-tag size="small" type="warning" effect="plain" round>演示数据</el-tag>
        </div>
        <h1>工作站概览</h1>
        <p>集中查看资源、运行状态、实时监控、系统信息与容器应用。</p>
      </div>
      <div class="page-heading__actions overview-heading__actions">
        <span>更新于 {{ updatedAt }}</span>
        <el-button :loading="refreshing" @click="refreshOverview()">
          <el-icon><Refresh /></el-icon>
          刷新数据
        </el-button>
      </div>
    </section>

    <section id="resources" class="resource-grid content-anchor" aria-label="资源概览">
      <article
        v-for="resource in snapshot.resources"
        :key="resource.key"
        class="resource-card"
        :class="`resource-card--${resource.key}`"
      >
        <span class="resource-card__icon">
          <el-icon><component :is="resourceIcons[resource.key]" /></el-icon>
        </span>
        <div class="resource-card__copy">
          <span>{{ resource.label }}</span>
          <strong
            >{{ resource.value }}<small>{{ resource.unit }}</small></strong
          >
          <p>{{ resource.detail }}</p>
        </div>
        <span class="resource-card__arrow"
          ><el-icon><ArrowRight /></el-icon
        ></span>
      </article>
    </section>

    <div class="overview-dashboard">
      <div class="overview-dashboard__main">
        <section id="status" class="panel overview-panel content-anchor">
          <header class="overview-panel__header">
            <div>
              <span class="panel__eyebrow">SYSTEM STATUS</span>
              <h2>运行状态</h2>
            </div>
            <el-tag type="success" effect="light" round>
              <span class="live-dot" /> 实时采样
            </el-tag>
          </header>

          <div class="status-grid">
            <article class="health-card">
              <span class="health-card__halo">
                <el-icon><CircleCheckFilled /></el-icon>
              </span>
              <div>
                <span>综合状态</span>
                <strong>{{ snapshot.healthMessage }}</strong>
                <p>{{ snapshot.healthDetail }}</p>
              </div>
            </article>

            <article v-for="metric in snapshot.usage" :key="metric.key" class="usage-card">
              <el-progress
                type="dashboard"
                :percentage="metric.percentage"
                :width="116"
                :stroke-width="8"
                :color="gaugeColor(metric.percentage)"
              >
                <template #default>
                  <strong>{{ metric.percentage.toFixed(1) }}%</strong>
                </template>
              </el-progress>
              <div class="usage-card__copy">
                <span>{{ metric.label }}</span>
                <strong>{{ metric.value }}</strong>
                <small>{{ metric.detail }}</small>
              </div>
            </article>
          </div>
        </section>

        <section id="monitoring" class="panel overview-panel monitoring-panel content-anchor">
          <header class="overview-panel__header monitoring-panel__header">
            <div>
              <span class="panel__eyebrow">TELEMETRY</span>
              <h2>实时监控</h2>
            </div>
            <div class="monitoring-toolbar">
              <el-segmented v-model="monitorMode" :options="monitorOptions" />
              <el-select v-model="selectedTelemetryFilter" class="monitoring-filter">
                <el-option
                  v-for="option in activeTelemetry.filters"
                  :key="option.value"
                  :label="option.label"
                  :value="option.value"
                />
              </el-select>
            </div>
          </header>

          <div class="telemetry-summary">
            <div
              v-for="stat in activeFilter.stats"
              :key="stat.label"
              class="telemetry-stat"
              :class="`telemetry-stat--${stat.tone}`"
            >
              <span>{{ stat.label }}</span>
              <strong>{{ stat.value }}</strong>
            </div>
            <div class="telemetry-legend" aria-label="图例">
              <span v-for="item in activeFilter.series" :key="item.name">
                <i :style="{ backgroundColor: item.color }" />{{ item.name }}
              </span>
            </div>
          </div>

          <TelemetryChart
            :key="`${monitorMode}-${selectedTelemetryFilter}`"
            :labels="activeTelemetry.labels"
            :series="activeFilter.series"
            :unit="activeTelemetry.unit"
          />
        </section>
      </div>

      <aside class="overview-dashboard__side">
        <section id="system" class="panel overview-panel content-anchor">
          <header class="overview-panel__header">
            <div>
              <span class="panel__eyebrow">HOST INFORMATION</span>
              <h2>系统信息</h2>
            </div>
            <el-tag effect="plain" round>Linux 工作站</el-tag>
          </header>

          <div class="system-grid">
            <article v-for="item in snapshot.system" :key="item.label" class="system-item">
              <span>{{ item.label }}</span>
              <strong>{{ item.value }}</strong>
              <small v-if="item.hint">{{ item.hint }}</small>
            </article>
          </div>
        </section>

        <section id="containers" class="panel overview-panel content-anchor">
          <header class="overview-panel__header application-header">
            <div>
              <span class="panel__eyebrow">XUANYUAN POPULAR</span>
              <h2>轩辕热门应用</h2>
              <p>后端接入后从轩辕镜像服务获取并缓存，当前为 Top 6 演示内容。</p>
            </div>
            <el-button @click="ElMessage.info('应用市场将在后端服务接入后开放')">
              查看更多
              <el-icon><ArrowRight /></el-icon>
            </el-button>
          </header>

          <div class="application-grid">
            <article
              v-for="app in snapshot.applications"
              :key="app.id"
              class="application-card"
              :class="{ 'application-card--installed': app.installedVersion }"
            >
              <div class="application-card__top">
                <span class="application-card__icon" :class="`application-card__icon--${app.icon}`">
                  <el-icon><component :is="applicationIcons[app.icon]" /></el-icon>
                </span>
                <span class="application-card__copy">
                  <strong>{{ app.name }}</strong>
                  <small>{{ app.category }}</small>
                </span>
                <el-tag
                  v-if="app.installedVersion"
                  :type="app.state === 'running' ? 'success' : 'info'"
                  effect="light"
                  round
                  size="small"
                >
                  {{ app.state === 'running' ? '运行中' : '已停止' }}
                </el-tag>
              </div>

              <p>{{ app.description }}</p>
              <div class="application-card__version">
                <span>推荐 {{ app.version }}</span>
                <span v-if="app.installedVersion">已装 {{ app.installedVersion }}</span>
              </div>

              <footer class="application-card__actions">
                <el-button plain @click="showDeferredAction('details', app)">更多</el-button>
                <el-button type="primary" plain @click="showDeferredAction('install', app)">
                  安装
                </el-button>
                <template v-if="app.installedVersion">
                  <el-button
                    plain
                    type="danger"
                    :disabled="app.state === 'stopped'"
                    @click="confirmContainerAction(app, 'stop')"
                  >
                    <el-icon><SwitchButton /></el-icon>
                    关闭
                  </el-button>
                  <el-button plain type="primary" @click="confirmContainerAction(app, 'restart')">
                    <el-icon><RefreshRight /></el-icon>
                    重启
                  </el-button>
                </template>
              </footer>
            </article>
          </div>
        </section>
      </aside>
    </div>
  </div>
</template>

<style scoped>
.overview-page {
  display: grid;
  gap: 20px;
}

.overview-heading {
  margin-bottom: 2px;
}

.overview-heading__actions {
  align-items: center;
}

.overview-heading__actions > span {
  color: var(--el-text-color-secondary);
  font-size: 11px;
}

.resource-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 14px;
}

.resource-card {
  --resource-color: var(--el-color-primary);
  position: relative;
  display: flex;
  overflow: hidden;
  align-items: center;
  min-height: 144px;
  padding: 20px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--whaledeck-panel-radius);
  background: var(--whaledeck-panel-bg);
  box-shadow: 0 8px 24px rgb(31 45 61 / 4%);
  transition:
    border-color 180ms ease,
    transform 180ms ease,
    box-shadow 180ms ease;
}

.resource-card::after {
  position: absolute;
  right: -34px;
  bottom: -50px;
  width: 130px;
  height: 130px;
  border-radius: 50%;
  background: color-mix(in srgb, var(--resource-color) 9%, transparent);
  content: '';
}

.resource-card:hover {
  border-color: color-mix(in srgb, var(--resource-color) 42%, var(--el-border-color));
  box-shadow: 0 14px 30px color-mix(in srgb, var(--resource-color) 10%, transparent);
  transform: translateY(-2px);
}

.resource-card--agents {
  --resource-color: #7c6df2;
}

.resource-card--websites {
  --resource-color: #409eff;
}

.resource-card--databases {
  --resource-color: #e6a23c;
}

.resource-card--containers {
  --resource-color: #36b5a0;
}

.resource-card__icon {
  display: grid;
  width: 48px;
  height: 48px;
  flex: 0 0 48px;
  place-items: center;
  border-radius: 14px;
  color: var(--resource-color);
  background: color-mix(in srgb, var(--resource-color) 12%, transparent);
  font-size: 22px;
}

.resource-card__copy {
  display: flex;
  min-width: 0;
  flex-direction: column;
  margin-left: 14px;
}

.resource-card__copy > span {
  color: var(--el-text-color-regular);
  font-size: 12px;
}

.resource-card__copy > strong {
  margin: 3px 0 2px;
  color: var(--el-text-color-primary);
  font-size: 30px;
  line-height: 1.15;
}

.resource-card__copy > strong small {
  margin-left: 5px;
  color: var(--el-text-color-secondary);
  font-size: 11px;
  font-weight: 500;
}

.resource-card__copy p {
  overflow: hidden;
  margin: 0;
  color: var(--el-text-color-secondary);
  font-size: 10px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.resource-card__arrow {
  position: absolute;
  z-index: 1;
  right: 16px;
  bottom: 14px;
  color: color-mix(in srgb, var(--resource-color) 70%, transparent);
}

.overview-dashboard {
  display: grid;
  grid-template-columns: minmax(0, 3fr) minmax(300px, 1fr);
  align-items: start;
  gap: 20px;
}

.overview-dashboard__main,
.overview-dashboard__side {
  display: grid;
  min-width: 0;
  gap: 20px;
}

.overview-panel {
  overflow: hidden;
}

.overview-panel__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  padding: 20px 22px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.overview-panel__header h2 {
  margin: 4px 0 0;
  font-size: 18px;
}

.overview-panel__header p {
  margin: 7px 0 0;
  color: var(--el-text-color-secondary);
  font-size: 11px;
}

.live-dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  margin-right: 4px;
  border-radius: 50%;
  background: currentColor;
  box-shadow: 0 0 0 4px color-mix(in srgb, currentColor 14%, transparent);
}

.status-grid {
  display: grid;
  grid-template-columns: 1.16fr repeat(3, 1fr);
}

.health-card,
.usage-card {
  display: flex;
  align-items: center;
  min-height: 174px;
  padding: 24px;
}

.usage-card {
  justify-content: center;
  gap: 14px;
  border-left: 1px solid var(--el-border-color-lighter);
}

.health-card__halo {
  display: grid;
  width: 74px;
  height: 74px;
  flex: 0 0 74px;
  place-items: center;
  border-radius: 50%;
  color: var(--el-color-success);
  background: color-mix(in srgb, var(--el-color-success) 12%, transparent);
  font-size: 34px;
  box-shadow: 0 0 0 10px color-mix(in srgb, var(--el-color-success) 5%, transparent);
}

.health-card > div {
  display: flex;
  min-width: 0;
  flex-direction: column;
  margin-left: 22px;
}

.health-card > div > span,
.usage-card__copy span {
  color: var(--el-text-color-secondary);
  font-size: 11px;
}

.health-card strong {
  margin: 4px 0;
  color: var(--el-color-success);
  font-size: 24px;
}

.health-card p {
  margin: 0;
  color: var(--el-text-color-secondary);
  font-size: 10px;
  line-height: 1.6;
}

.usage-card :deep(.el-progress__text) {
  color: var(--el-text-color-primary);
}

.usage-card :deep(.el-progress__text strong) {
  font-size: 17px;
}

.usage-card__copy {
  display: flex;
  min-width: 0;
  flex-direction: column;
}

.usage-card__copy strong {
  margin: 5px 0;
  font-size: 12px;
}

.usage-card__copy small {
  color: var(--el-text-color-secondary);
  font-size: 9px;
}

.monitoring-panel__header {
  align-items: center;
}

.monitoring-toolbar {
  display: flex;
  align-items: center;
  gap: 10px;
}

.monitoring-filter {
  width: 224px;
}

.telemetry-summary {
  display: flex;
  align-items: stretch;
  gap: 10px;
  padding: 18px 22px 4px;
}

.telemetry-stat {
  --stat-color: var(--el-color-info);
  min-width: 124px;
  padding: 9px 11px;
  border: 1px solid color-mix(in srgb, var(--stat-color) 20%, var(--el-border-color-lighter));
  border-radius: 9px;
  background: color-mix(in srgb, var(--stat-color) 6%, var(--whaledeck-panel-bg));
}

.telemetry-stat--primary {
  --stat-color: var(--el-color-primary);
}

.telemetry-stat--success {
  --stat-color: var(--el-color-success);
}

.telemetry-stat--warning {
  --stat-color: var(--el-color-warning);
}

.telemetry-stat span,
.telemetry-stat strong {
  display: block;
}

.telemetry-stat span {
  color: var(--el-text-color-secondary);
  font-size: 9px;
}

.telemetry-stat strong {
  margin-top: 3px;
  color: var(--stat-color);
  font-size: 11px;
}

.telemetry-legend {
  display: flex;
  flex: 1;
  align-items: center;
  justify-content: flex-end;
  gap: 14px;
  color: var(--el-text-color-secondary);
  font-size: 10px;
}

.telemetry-legend span {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.telemetry-legend i {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.system-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  padding: 8px 16px 18px;
}

.system-item {
  display: flex;
  min-height: 88px;
  flex-direction: column;
  justify-content: center;
  padding: 14px 12px;
  border-right: 1px solid var(--el-border-color-lighter);
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.system-item:nth-child(2n) {
  border-right: 0;
}

.system-item:nth-last-child(-n + 2) {
  border-bottom: 0;
}

.system-item span {
  color: var(--el-text-color-secondary);
  font-size: 10px;
}

.system-item strong {
  overflow: hidden;
  margin-top: 7px;
  font-size: 13px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.system-item small {
  margin-top: 5px;
  color: var(--el-color-primary);
  font-size: 9px;
}

.application-header {
  align-items: flex-start;
  flex-direction: column;
}

.application-header .el-button {
  width: 100%;
}

.application-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 12px;
  padding: 16px;
}

.application-card {
  display: flex;
  min-width: 0;
  min-height: 0;
  flex-direction: column;
  padding: 16px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 12px;
  background: color-mix(in srgb, var(--whaledeck-soft-bg) 56%, var(--whaledeck-panel-bg));
  transition:
    border-color 180ms ease,
    box-shadow 180ms ease,
    transform 180ms ease;
}

.application-card:hover {
  border-color: color-mix(in srgb, var(--el-color-primary) 32%, var(--el-border-color));
  box-shadow: 0 10px 26px rgb(31 45 61 / 7%);
  transform: translateY(-2px);
}

.application-card--installed {
  border-color: color-mix(in srgb, var(--el-color-success) 32%, var(--el-border-color-lighter));
}

.application-card__top {
  display: flex;
  align-items: center;
  gap: 11px;
}

.application-card__icon {
  --app-color: var(--el-color-primary);
  display: grid;
  width: 42px;
  height: 42px;
  flex: 0 0 42px;
  place-items: center;
  border: 1px solid color-mix(in srgb, var(--app-color) 24%, transparent);
  border-radius: 11px;
  color: var(--app-color);
  background: color-mix(in srgb, var(--app-color) 10%, var(--whaledeck-panel-bg));
  font-size: 20px;
  box-shadow: 0 5px 14px color-mix(in srgb, var(--app-color) 10%, transparent);
}

.application-card__icon--gateway {
  --app-color: #38a169;
}

.application-card__icon--database {
  --app-color: #3b82b5;
}

.application-card__icon--cache {
  --app-color: #dc3c32;
}

.application-card__icon--ai {
  --app-color: #4361ee;
}

.application-card__icon--runtime {
  --app-color: #7c6df2;
}

.application-card__icon--developer {
  --app-color: #1f2937;
}

.dark .application-card__icon--developer {
  --app-color: #d8dee9;
}

.application-card__copy {
  display: flex;
  min-width: 0;
  flex: 1;
  flex-direction: column;
}

.application-card__copy strong {
  overflow: hidden;
  font-size: 13px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.application-card__copy small {
  margin-top: 3px;
  color: var(--el-text-color-secondary);
  font-size: 9px;
}

.application-card > p {
  margin: 12px 0 9px;
  color: var(--el-text-color-secondary);
  font-size: 10px;
  line-height: 1.7;
}

.application-card__version {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  color: var(--el-text-color-secondary);
  font-size: 9px;
}

.application-card__version span {
  padding: 3px 7px;
  border-radius: 5px;
  background: var(--whaledeck-hover-bg);
}

.application-card__actions {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
  margin-top: 12px;
}

.application-card__actions :deep(.el-button) {
  width: 100%;
  padding-right: 8px;
  padding-left: 8px;
  margin-left: 0;
}

@media (max-width: 1260px) {
  .resource-grid,
  .status-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .usage-card:nth-child(3) {
    border-top: 1px solid var(--el-border-color-lighter);
    border-left: 0;
  }

  .usage-card:nth-child(4) {
    border-top: 1px solid var(--el-border-color-lighter);
  }
}

@media (max-width: 1180px) {
  .overview-dashboard {
    grid-template-columns: 1fr;
  }

  .system-grid {
    grid-template-columns: 1fr;
  }

  .system-item:nth-child(n) {
    border-right: 0;
    border-bottom: 1px solid var(--el-border-color-lighter);
  }

  .system-item:last-child {
    border-bottom: 0;
  }
}

@media (max-width: 1040px) {
  .telemetry-summary {
    flex-wrap: wrap;
  }

  .telemetry-legend {
    min-width: 100%;
    justify-content: flex-start;
    padding: 4px 2px;
  }
}

@media (max-width: 760px) {
  .overview-heading__actions {
    align-items: flex-start;
  }

  .overview-panel__header,
  .monitoring-panel__header,
  .application-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .monitoring-toolbar {
    width: 100%;
    align-items: stretch;
    flex-direction: column;
  }

  .monitoring-filter {
    width: 100%;
  }

  .telemetry-stat {
    min-width: calc(50% - 5px);
    flex: 1;
  }

  .application-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 560px) {
  .resource-grid,
  .status-grid,
  .system-grid {
    grid-template-columns: 1fr;
  }

  .resource-card {
    min-height: 126px;
  }

  .usage-card,
  .usage-card:nth-child(n) {
    border-top: 1px solid var(--el-border-color-lighter);
    border-left: 0;
  }

  .system-item:nth-child(n) {
    border-right: 0;
    border-bottom: 1px solid var(--el-border-color-lighter);
  }

  .system-item:last-child {
    border-bottom: 0;
  }

  .telemetry-stat {
    min-width: 100%;
  }
}

@media (prefers-reduced-motion: reduce) {
  .resource-card,
  .application-card {
    transition: none;
  }
}
</style>
