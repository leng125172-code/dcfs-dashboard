<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import {
  Delete,
  Download,
  Plus,
  RefreshRight,
  VideoPause,
  VideoPlay,
} from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useRoute } from 'vue-router'
import { ApiError, apiRequest } from '@/services/apiClient'
import type { ManagedResource } from '@/services/contracts'
import { enqueueOperation, planOperation } from '@/services/operations'

const route = useRoute()
const loading = ref(false)
const error = ref('')
const traceId = ref('')
const resources = ref<ManagedResource[]>([])
const pullDialogVisible = ref(false)
const networkDialogVisible = ref(false)
const imageReference = ref('')
const networkForm = ref({ name: '', internal: false, attachable: true })
const actionRunning = ref('')
const title = computed(() => route.meta.title || '资源')
const endpoint = computed(() => String(route.meta.endpoint || ''))
const isImages = computed(() => endpoint.value === 'images')
const isNetworks = computed(() => endpoint.value === 'networks')
const isVolumes = computed(() => endpoint.value === 'volumes')
const isSystemd = computed(() => endpoint.value === 'systemd')

function actionsFor(resource: ManagedResource) {
  return (resource.attributes.allowedActions || '')
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
}

async function load() {
  const endpoint = route.meta.endpoint
  if (!endpoint) return
  loading.value = true
  error.value = ''
  try {
    const value = await apiRequest<ManagedResource[] | { applications: ManagedResource[] }>(
      endpoint,
    )
    resources.value = Array.isArray(value) ? value : value.applications
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : '读取资源失败'
    traceId.value = reason instanceof ApiError ? reason.problem.traceId || '' : ''
  } finally {
    loading.value = false
  }
}

async function pullImage() {
  const image = imageReference.value.trim()
  if (!image) {
    ElMessage.warning('请输入 OCI 镜像名称和标签。')
    return
  }
  actionRunning.value = 'pull'
  try {
    const job = await enqueueOperation('containers', 'pull', null, { image }, false)
    pullDialogVisible.value = false
    imageReference.value = ''
    ElMessage.success(`镜像拉取任务已提交：${job.id}`)
  } finally {
    actionRunning.value = ''
  }
}

async function pruneImages() {
  const parameters = { images: 'true', volumes: 'false' }
  actionRunning.value = 'prune'
  try {
    const plan = await planOperation('containers', 'prune', null, parameters)
    await ElMessageBox.confirm(
      [...plan.changes, ...plan.warnings, '仅清理未使用的悬空镜像，不清理数据卷。'].join('\n'),
      '清理镜像确认',
      { type: 'warning', confirmButtonText: '确认清理' },
    )
    const job = await enqueueOperation('containers', 'prune', null, parameters, true, plan.planHash)
    ElMessage.success(`清理任务已提交：${job.id}`)
  } finally {
    actionRunning.value = ''
  }
}

async function createNetwork() {
  const parameters = {
    name: networkForm.value.name.trim(),
    internal: String(networkForm.value.internal),
    attachable: String(networkForm.value.attachable),
  }
  if (!parameters.name) {
    ElMessage.warning('请输入网络名称。')
    return
  }
  actionRunning.value = 'network-create'
  try {
    const plan = await planOperation('docker', 'network-create', 'new', parameters)
    const job = await enqueueOperation(
      'docker',
      'network-create',
      'new',
      parameters,
      false,
      plan.planHash,
    )
    networkDialogVisible.value = false
    networkForm.value = { name: '', internal: false, attachable: true }
    ElMessage.success(`网络创建任务已提交：${job.id}`)
  } finally {
    actionRunning.value = ''
  }
}

async function deleteNetwork(resource: ManagedResource) {
  const parameters: Record<string, string> = {}
  const plan = await planOperation('docker', 'network-delete', resource.id, parameters)
  await ElMessageBox.confirm(
    [...plan.changes, ...plan.warnings].join('\n'),
    `删除网络 ${resource.name}`,
    { type: 'warning', confirmButtonText: '删除网络', cancelButtonText: '取消' },
  )
  const job = await enqueueOperation(
    'docker',
    'network-delete',
    resource.id,
    parameters,
    true,
    plan.planHash,
  )
  ElMessage.success(`网络删除任务已提交：${job.id}`)
}

async function deleteVolume(resource: ManagedResource) {
  const confirmation = await ElMessageBox.prompt(
    '数据卷中的内容将永久删除，且本操作不提供恢复。请输入数据卷名称确认。',
    `删除数据卷 ${resource.name}`,
    {
      type: 'error',
      confirmButtonText: '永久删除',
      cancelButtonText: '取消',
      inputValidator: (value) => value === resource.name || '输入的数据卷名称不匹配',
    },
  )
  const parameters: Record<string, string> = { confirmationName: confirmation.value }
  const plan = await planOperation('docker', 'volume-delete', resource.id, parameters)
  const job = await enqueueOperation(
    'docker',
    'volume-delete',
    resource.id,
    parameters,
    true,
    plan.planHash,
  )
  ElMessage.success(`数据卷删除任务已提交：${job.id}`)
}

async function changeSystemd(resource: ManagedResource, verb: 'start' | 'stop' | 'restart') {
  const action = `systemd-${verb}`
  const label = verb === 'start' ? '启动' : verb === 'stop' ? '停止' : '重启'
  actionRunning.value = `${resource.id}:${verb}`
  try {
    await ElMessageBox.confirm(
      `${label} ${resource.name}？系统服务状态将在任务中心持续更新。`,
      `${label}系统服务`,
      {
        type: verb === 'start' ? 'info' : 'warning',
        confirmButtonText: `确认${label}`,
      },
    )
    const job = await enqueueOperation('host', action, resource.id, {}, verb !== 'start')
    ElMessage.success(`${label}任务已提交：${job.id}`)
  } finally {
    actionRunning.value = ''
  }
}

onMounted(load)
watch(() => route.fullPath, load)
</script>

<template>
  <div class="resource-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">MANAGEMENT</span>
        <h1>{{ title }}</h1>
        <p>读取工作站上的真实资源状态；管理动作只对管理员开放。</p>
      </div>
      <el-button :loading="loading" @click="load"
        ><el-icon><RefreshRight /></el-icon>刷新</el-button
      >
      <div v-if="isImages" class="heading-actions">
        <el-button type="primary" @click="pullDialogVisible = true"
          ><el-icon><Download /></el-icon>拉取镜像</el-button
        >
        <el-button :loading="actionRunning === 'prune'" @click="pruneImages"
          ><el-icon><Delete /></el-icon>清理悬空镜像</el-button
        >
      </div>
      <div v-if="isNetworks" class="heading-actions">
        <el-button type="primary" @click="networkDialogVisible = true"
          ><el-icon><Plus /></el-icon>创建网络</el-button
        >
      </div>
    </header>
    <el-alert
      v-if="error"
      :title="error"
      type="error"
      :description="traceId ? `追踪号：${traceId}` : undefined"
      show-icon
      :closable="false"
    />
    <section class="panel resource-table-panel" v-loading="loading">
      <el-empty
        v-if="!loading && !error && resources.length === 0"
        description="没有可显示的资源"
      />
      <el-table v-else :data="resources" row-key="id">
        <el-table-column prop="name" label="名称" min-width="220" />
        <el-table-column prop="type" label="类型" min-width="150" />
        <el-table-column prop="state" label="状态" min-width="120"
          ><template #default="scope"
            ><el-tag effect="plain">{{ scope.row.state }}</el-tag></template
          ></el-table-column
        >
        <el-table-column prop="version" label="版本 / 镜像" min-width="220" show-overflow-tooltip />
        <el-table-column v-if="isNetworks || isVolumes" label="详情" min-width="220">
          <template #default="scope">
            <template v-if="isNetworks">
              {{ scope.row.attributes.containerCount || '0' }} 个容器 ·
              {{ scope.row.attributes.internal === 'true' ? '内部网络' : '外部网络' }}
            </template>
            <template v-else>
              {{ scope.row.attributes.referenceCount || '0' }} 个引用 ·
              {{
                scope.row.attributes.sizeBytes === '-1'
                  ? '容量未知'
                  : `${scope.row.attributes.sizeBytes} B`
              }}
            </template>
          </template>
        </el-table-column>
        <el-table-column label="保护" width="110"
          ><template #default="scope"
            ><el-tag :type="scope.row.isProtected ? 'warning' : 'info'" effect="light">{{
              scope.row.isProtected ? '受保护' : '普通'
            }}</el-tag></template
          ></el-table-column
        >
        <el-table-column
          v-if="isSystemd || isNetworks || isVolumes"
          label="操作"
          min-width="210"
          fixed="right"
        >
          <template #default="scope">
            <el-button
              v-if="actionsFor(scope.row).includes('systemd-start')"
              text
              type="primary"
              :loading="actionRunning === `${scope.row.id}:start`"
              @click="changeSystemd(scope.row, 'start')"
              ><el-icon><VideoPlay /></el-icon>启动</el-button
            >
            <el-button
              v-if="actionsFor(scope.row).includes('systemd-stop')"
              text
              type="danger"
              :loading="actionRunning === `${scope.row.id}:stop`"
              @click="changeSystemd(scope.row, 'stop')"
              ><el-icon><VideoPause /></el-icon>停止</el-button
            >
            <el-button
              v-if="actionsFor(scope.row).includes('systemd-restart')"
              text
              type="primary"
              :loading="actionRunning === `${scope.row.id}:restart`"
              @click="changeSystemd(scope.row, 'restart')"
              ><el-icon><RefreshRight /></el-icon>重启</el-button
            >
            <el-button
              v-if="isNetworks"
              text
              type="danger"
              :disabled="scope.row.isProtected || scope.row.attributes.containerCount !== '0'"
              @click="deleteNetwork(scope.row)"
              ><el-icon><Delete /></el-icon>删除</el-button
            >
            <el-button
              v-if="isVolumes"
              text
              type="danger"
              :disabled="scope.row.isProtected || scope.row.state !== 'Orphaned'"
              @click="deleteVolume(scope.row)"
              ><el-icon><Delete /></el-icon>删除</el-button
            >
          </template>
        </el-table-column>
      </el-table>
    </section>
    <el-dialog v-model="pullDialogVisible" title="拉取 OCI 镜像" width="min(520px, 92vw)">
      <el-form label-position="top" @submit.prevent="pullImage">
        <el-form-item label="镜像名称">
          <el-input
            v-model="imageReference"
            placeholder="例如：docker.io/library/nginx:1.27-alpine"
            autocomplete="off"
            @keyup.enter="pullImage"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="pullDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="actionRunning === 'pull'" @click="pullImage"
          >提交拉取</el-button
        >
      </template>
    </el-dialog>
    <el-dialog v-model="networkDialogVisible" title="创建自定义网络" width="min(520px, 92vw)">
      <el-form label-position="top" @submit.prevent="createNetwork">
        <el-form-item label="网络名称">
          <el-input v-model="networkForm.name" placeholder="team-apps" autocomplete="off" />
        </el-form-item>
        <el-form-item label="内部网络">
          <el-switch v-model="networkForm.internal" />
          <span class="form-hint">内部网络不提供默认外部连通能力。</span>
        </el-form-item>
        <el-form-item label="允许手动接入">
          <el-switch v-model="networkForm.attachable" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="networkDialogVisible = false">取消</el-button>
        <el-button
          type="primary"
          :loading="actionRunning === 'network-create'"
          @click="createNetwork"
          >预检并创建</el-button
        >
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.resource-page {
  display: grid;
  gap: 18px;
}
.resource-table-panel {
  min-height: 280px;
  padding: 14px;
}
.heading-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.heading-actions .el-button + .el-button {
  margin-left: 0;
}
</style>
