<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { apiRequest } from '@/services/apiClient'
import type { ManagedResource } from '@/services/contracts'
import { enqueueOperation, planOperation } from '@/services/operations'

const timer = ref<ManagedResource | null>(null)
const loading = ref(false)
const running = ref('')

async function load() {
  loading.value = true
  try {
    const resources = await apiRequest<ManagedResource[]>('systemd')
    timer.value = resources.find((item) => item.id === 'database-platform.update') || null
  } finally {
    loading.value = false
  }
}

async function checkUpdates() {
  running.value = 'update-check'
  try {
    const job = await enqueueOperation('host', 'update-check', 'whaledeck.host', {}, false)
    ElMessage.success(`更新检查已提交：${job.id}`)
  } finally {
    running.value = ''
  }
}

async function critical(action: 'update-install' | 'reboot') {
  running.value = action
  try {
    const plan = await planOperation('host', action, 'whaledeck.host', {})
    const reboot = action === 'reboot'
    await ElMessageBox.confirm(
      [
        ...plan.changes,
        ...plan.warnings,
        reboot ? '工作站会短暂离线。' : '将安装工作站可用的软件包更新。',
      ].join('\n'),
      reboot ? '重启工作站确认' : '安装系统更新确认',
      { type: 'warning', confirmButtonText: reboot ? '确认重启' : '确认安装' },
    )
    const job = await enqueueOperation('host', action, 'whaledeck.host', {}, true, plan.planHash)
    ElMessage.success(`维护任务已提交：${job.id}`)
  } finally {
    running.value = ''
  }
}

onMounted(load)
</script>

<template>
  <div class="host-updates-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">SYSTEM</span>
        <h1>系统更新</h1>
        <p>检查、安装更新并管理周日 20:00 的自动更新任务。</p>
      </div>
    </header>
    <section class="updates-grid" v-loading="loading">
      <article class="panel update-card">
        <h2>自动更新</h2>
        <p>systemd 定时器由 database-platform 仓库维护。</p>
        <el-descriptions :column="1" border>
          <el-descriptions-item label="服务">{{
            timer?.name || 'database-platform-workstation-update.timer'
          }}</el-descriptions-item>
          <el-descriptions-item label="状态"
            ><el-tag effect="plain">{{ timer?.state || '未知' }}</el-tag></el-descriptions-item
          >
          <el-descriptions-item label="启用状态">{{
            timer?.version || '未知'
          }}</el-descriptions-item>
        </el-descriptions>
      </article>
      <article class="panel update-card">
        <h2>手动维护</h2>
        <p>所有动作进入任务中心；安装更新和重启必须先预检并二次确认。</p>
        <div class="update-actions">
          <el-button type="primary" :loading="running === 'update-check'" @click="checkUpdates"
            >检查更新</el-button
          >
          <el-button :loading="running === 'update-install'" @click="critical('update-install')"
            >安装更新</el-button
          >
          <el-button type="danger" plain :loading="running === 'reboot'" @click="critical('reboot')"
            >重启工作站</el-button
          >
        </div>
      </article>
    </section>
  </div>
</template>

<style scoped>
.host-updates-page {
  display: grid;
  gap: 18px;
}
.updates-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 14px;
}
.update-card {
  padding: 22px;
}
.update-card h2 {
  margin: 0;
  font-size: 16px;
  font-weight: 500;
}
.update-card > p {
  min-height: 42px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  line-height: 1.7;
}
.update-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.update-actions .el-button + .el-button {
  margin-left: 0;
}
@media (max-width: 760px) {
  .updates-grid {
    grid-template-columns: 1fr;
  }
}
</style>
