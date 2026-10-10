<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { apiRequest } from '@/services/apiClient'
import type { AgentStatus } from '@/services/contracts'

const status = ref<AgentStatus | null>(null)
const loading = ref(false)
async function load() {
  loading.value = true
  try {
    status.value = await apiRequest<AgentStatus>('agents')
  } finally {
    loading.value = false
  }
}
onMounted(load)
</script>

<template>
  <div class="agent-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">HOST AGENT</span>
        <h1>宿主机 Agent</h1>
        <p>独立于 Docker 运行的主机控制通道、能力与当前任务。</p>
      </div>
      <el-button :loading="loading" @click="load">刷新</el-button>
    </header>
    <section v-loading="loading" class="panel agent-panel" v-if="status">
      <el-descriptions :column="3" border>
        <el-descriptions-item label="状态"
          ><el-tag :type="status.health.available ? 'success' : 'danger'">{{
            status.health.status
          }}</el-tag></el-descriptions-item
        >
        <el-descriptions-item label="Agent 版本"
          >V{{ status.capabilities.agentVersion }}</el-descriptions-item
        >
        <el-descriptions-item label="RPC 协议"
          >{{ status.capabilities.protocolMajor }}.{{
            status.capabilities.protocolMinor
          }}</el-descriptions-item
        >
        <el-descriptions-item label="Docker">{{
          status.health.dockerAvailable ? '可用' : '不可用'
        }}</el-descriptions-item>
        <el-descriptions-item label="systemd">{{
          status.health.systemdAvailable ? '可用' : '不可用'
        }}</el-descriptions-item>
        <el-descriptions-item label="最近心跳">{{
          new Date(status.health.observedAtUtc).toLocaleString()
        }}</el-descriptions-item>
      </el-descriptions>
      <div>
        <h2>能力</h2>
        <div class="capabilities">
          <el-tag v-for="item in status.capabilities.capabilities" :key="item" effect="plain">{{
            item
          }}</el-tag>
        </div>
      </div>
      <div>
        <h2>当前任务</h2>
        <el-table :data="status.currentJobs" empty-text="当前没有运行中的任务"
          ><el-table-column prop="jobType" label="任务" min-width="180" /><el-table-column
            prop="state"
            label="状态"
            width="110" /><el-table-column
            prop="phase"
            label="阶段"
            min-width="150" /><el-table-column prop="progressPercent" label="进度" width="90"
        /></el-table>
      </div>
    </section>
  </div>
</template>

<style scoped>
.agent-page,
.agent-panel {
  display: grid;
  gap: 18px;
}
.agent-panel {
  padding: 20px;
}
.agent-panel h2 {
  margin: 0 0 10px;
  font-size: 16px;
}
.capabilities {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
</style>
