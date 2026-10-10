<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { Box, Delete, Plus, RefreshRight, VideoPause, VideoPlay } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { apiRequest } from '@/services/apiClient'
import { enqueueOperation, planOperation } from '@/services/operations'
import type { ContainerLogs, ContainerStats } from '@/services/contracts'

interface Container {
  id: string
  name: string
  image: string
  state: string
  status: string
  isProtected: boolean
  protectionLevel: string
  labels: Record<string, string>
}

const items = ref<Container[]>([])
const loading = ref(false)
const createVisible = ref(false)
const submitting = ref(false)
const form = reactive({ name: '', image: '', cpus: 1, memoryMb: 512, autoUpdate: false })
const detailVisible = ref(false)
const detailLoading = ref(false)
const selected = ref<Container | null>(null)
const logs = ref<ContainerLogs>({ lines: [], truncated: false })
const stats = ref<Record<string, string>>({})

async function load() {
  loading.value = true
  try {
    items.value = await apiRequest<Container[]>('containers?includeStopped=true')
  } finally {
    loading.value = false
  }
}

async function lifecycle(item: Container, action: 'start' | 'stop' | 'restart') {
  if (item.isProtected) {
    ElMessage.warning('受保护容器只能通过平台维护流程操作')
    return
  }
  const label = action === 'start' ? '启动' : action === 'stop' ? '停止' : '重启'
  await ElMessageBox.confirm(`${label}容器“${item.name}”？`, `${label}确认`, {
    type: 'warning',
    confirmButtonText: label,
    cancelButtonText: '取消',
  })
  const job = await enqueueOperation(
    'containers',
    action,
    item.id,
    { timeoutSeconds: '10' },
    action !== 'start',
  )
  ElMessage.success(`任务已提交：${job.id}`)
  await load()
}

async function removeContainer(item: Container) {
  if (item.isProtected) {
    ElMessage.warning('受保护容器不能从普通容器页删除')
    return
  }
  const parameters = { preserveVolumes: 'true', timeoutSeconds: '10' }
  const plan = await planOperation('containers', 'delete', item.id, parameters)
  await ElMessageBox.confirm(
    [...plan.changes, ...plan.warnings, '容器会被删除；匿名卷、镜像和应用数据默认保留。'].join(
      '\n',
    ),
    `删除容器 ${item.name}`,
    { type: 'error', confirmButtonText: '删除容器', cancelButtonText: '取消' },
  )
  const job = await enqueueOperation(
    'containers',
    'delete',
    item.id,
    parameters,
    true,
    plan.planHash,
  )
  ElMessage.success(`删除任务已提交：${job.id}`)
}

async function createContainer() {
  submitting.value = true
  try {
    const parameters = {
      name: form.name.trim(),
      image: form.image.trim(),
      cpus: String(form.cpus),
      memoryMb: String(form.memoryMb),
      autoUpdate: String(form.autoUpdate),
    }
    const plan = await planOperation('containers', 'create', 'new', parameters)
    const details =
      [...plan.changes, ...plan.warnings].join('\n') ||
      '将拉取镜像、创建并启动受 Whale Deck 管理的容器。'
    await ElMessageBox.confirm(details, '确认创建容器', {
      type: 'warning',
      confirmButtonText: '创建',
      cancelButtonText: '取消',
    })
    const job = await enqueueOperation(
      'containers',
      'create',
      'new',
      parameters,
      true,
      plan.planHash,
    )
    createVisible.value = false
    ElMessage.success(`创建任务已提交：${job.id}`)
  } finally {
    submitting.value = false
  }
}

async function openDetails(item: Container) {
  selected.value = item
  detailVisible.value = true
  detailLoading.value = true
  logs.value = { lines: [], truncated: false }
  stats.value = {}
  try {
    const id = encodeURIComponent(item.id)
    const [logResult, statsResult] = await Promise.all([
      apiRequest<ContainerLogs>(`containers/${id}/logs?tail=500&sinceMinutes=60`),
      item.state === 'running'
        ? apiRequest<ContainerStats>(`containers/${id}/stats`)
        : Promise.resolve({ values: {} }),
    ])
    logs.value = logResult
    stats.value = statsResult.values
  } finally {
    detailLoading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="containers-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">DOCKER</span>
        <h1>容器</h1>
        <p>受保护的基础容器不会出现在普通生命周期操作中。</p>
      </div>
      <div class="page-heading__actions">
        <el-button :loading="loading" @click="load"
          ><el-icon><RefreshRight /></el-icon>刷新</el-button
        ><el-button type="primary" @click="createVisible = true"
          ><el-icon><Plus /></el-icon>创建容器</el-button
        >
      </div>
    </header>
    <section class="container-grid" v-loading="loading">
      <article v-for="item in items" :key="item.id" class="panel container-card">
        <span class="container-card__icon"
          ><el-icon><Box /></el-icon
        ></span>
        <div>
          <strong>{{ item.name }}</strong
          ><small>{{ item.image }}</small
          ><span>{{ item.status }}</span>
        </div>
        <el-tag :type="item.state === 'running' ? 'success' : 'info'" effect="light">{{
          item.state
        }}</el-tag>
        <el-tag v-if="item.isProtected" type="warning" effect="plain">{{
          item.protectionLevel
        }}</el-tag>
        <div class="container-card__actions">
          <el-button link type="primary" @click="openDetails(item)">详情</el-button>
          <el-button
            v-if="item.state !== 'running'"
            link
            type="primary"
            :disabled="item.isProtected"
            @click="lifecycle(item, 'start')"
            ><el-icon><VideoPlay /></el-icon>启动</el-button
          >
          <template v-else
            ><el-button
              link
              type="danger"
              :disabled="item.isProtected"
              @click="lifecycle(item, 'stop')"
              ><el-icon><VideoPause /></el-icon>停止</el-button
            ><el-button
              link
              type="primary"
              :disabled="item.isProtected"
              @click="lifecycle(item, 'restart')"
              ><el-icon><RefreshRight /></el-icon>重启</el-button
            ></template
          >
          <el-button link type="danger" :disabled="item.isProtected" @click="removeContainer(item)"
            ><el-icon><Delete /></el-icon>删除</el-button
          >
        </div>
      </article>
    </section>
    <el-dialog v-model="createVisible" title="创建受管容器" width="min(560px, calc(100vw - 32px))">
      <el-alert
        title="仅支持无宿主端口、无宿主目录挂载的安全基础配置；复杂应用请使用应用模板。"
        type="info"
        :closable="false"
        show-icon
      />
      <el-form label-position="top" class="container-form">
        <el-form-item label="容器名称"
          ><el-input v-model="form.name" placeholder="my-application"
        /></el-form-item>
        <el-form-item label="OCI 镜像"
          ><el-input v-model="form.image" placeholder="nginx:1.29-alpine"
        /></el-form-item>
        <div class="container-form__resources">
          <el-form-item label="CPU"
            ><el-input-number v-model="form.cpus" :min="0.1" :max="32" :step="0.5" /></el-form-item
          ><el-form-item label="内存 (MiB)"
            ><el-input-number v-model="form.memoryMb" :min="32" :max="32768" :step="128"
          /></el-form-item>
        </div>
        <el-form-item label="自动更新"
          ><el-switch v-model="form.autoUpdate" /><span class="form-hint"
            >只有启用此标签的普通应用才进入自动更新。</span
          ></el-form-item
        >
      </el-form>
      <template #footer
        ><el-button @click="createVisible = false">取消</el-button
        ><el-button
          type="primary"
          :loading="submitting"
          :disabled="!form.name || !form.image"
          @click="createContainer"
          >预检并创建</el-button
        ></template
      >
    </el-dialog>
    <el-drawer
      v-model="detailVisible"
      :title="selected ? `容器详情 · ${selected.name}` : '容器详情'"
      size="min(760px, 96vw)"
    >
      <div v-loading="detailLoading" class="container-detail">
        <el-descriptions v-if="selected" :column="2" border>
          <el-descriptions-item label="镜像" :span="2">{{ selected.image }}</el-descriptions-item>
          <el-descriptions-item label="状态">{{ selected.status }}</el-descriptions-item>
          <el-descriptions-item label="保护级别">{{
            selected.protectionLevel
          }}</el-descriptions-item>
          <el-descriptions-item label="CPU">{{ stats.cpu || '—' }}</el-descriptions-item>
          <el-descriptions-item label="内存"
            >{{ stats.memory || '—' }} · {{ stats.memoryPercent || '—' }}</el-descriptions-item
          >
          <el-descriptions-item label="网络 I/O">{{ stats.network || '—' }}</el-descriptions-item>
          <el-descriptions-item label="磁盘 I/O">{{ stats.block || '—' }}</el-descriptions-item>
          <el-descriptions-item label="进程数">{{ stats.pids || '—' }}</el-descriptions-item>
        </el-descriptions>
        <div class="log-heading">
          <h3>最近一小时日志</h3>
          <el-tag v-if="logs.truncated" type="warning" effect="plain">已按 64 KiB 截断</el-tag>
        </div>
        <pre class="container-logs">{{ logs.lines.join('\n') || '当前时间范围内没有日志。' }}</pre>
      </div>
    </el-drawer>
  </div>
</template>

<style scoped>
.containers-page {
  display: grid;
  gap: 18px;
}
.container-grid {
  display: grid;
  gap: 10px;
  min-height: 240px;
}
.container-card {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto auto auto;
  align-items: center;
  gap: 14px;
  padding: 14px 16px;
}
.container-card__icon {
  display: grid;
  width: 42px;
  height: 42px;
  place-items: center;
  border-radius: 10px;
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}
.container-card > div:nth-child(2) {
  display: grid;
  min-width: 0;
}
.container-card small {
  overflow: hidden;
  color: var(--el-text-color-secondary);
  text-overflow: ellipsis;
  white-space: nowrap;
}
.container-card span {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
.container-card__actions {
  display: flex;
}
.container-form {
  margin-top: 18px;
}
.container-form__resources {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}
.form-hint {
  margin-left: 10px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
.container-detail {
  display: grid;
  gap: 18px;
  min-height: 320px;
}
.log-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.log-heading h3 {
  margin: 0;
  font-size: 14px;
}
.container-logs {
  max-height: 52vh;
  overflow: auto;
  margin: 0;
  padding: 14px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  color: var(--el-text-color-regular);
  background: var(--el-fill-color-light);
  font: inherit;
  font-size: 12px;
  line-height: 1.65;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}
@media (max-width: 760px) {
  .container-card {
    grid-template-columns: auto minmax(0, 1fr) auto;
  }
  .container-card > .el-tag:nth-of-type(2) {
    display: none;
  }
  .container-card__actions {
    grid-column: 2 / -1;
  }
  .container-form__resources {
    grid-template-columns: 1fr;
  }
}
</style>
