<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref } from 'vue'
import { apiRequest } from '@/services/apiClient'
import type { Job } from '@/services/contracts'

const jobs = ref<Job[]>([])
const loading = ref(false)
let timer: number | undefined

async function load() {
  loading.value = true
  try { jobs.value = await apiRequest<Job[]>('jobs?take=100') } finally { loading.value = false }
}

onMounted(() => { void load(); timer = window.setInterval(load, 6000) })
onBeforeUnmount(() => window.clearInterval(timer))
</script>

<template>
  <div class="jobs-page">
    <header class="page-heading"><div><span class="panel__eyebrow">OPERATIONS</span><h1>任务中心</h1><p>异步操作每 6 秒刷新；任务最终状态以 Agent 回报为准。</p></div></header>
    <section class="panel" v-loading="loading">
      <el-table :data="jobs" row-key="id" empty-text="暂无任务">
        <el-table-column prop="jobType" label="任务" min-width="210" />
        <el-table-column prop="state" label="状态" width="130" />
        <el-table-column prop="phase" label="阶段" min-width="180" />
        <el-table-column label="进度" width="180"><template #default="scope"><el-progress :percentage="scope.row.progressPercent ?? 0" /></template></el-table-column>
        <el-table-column prop="errorCode" label="错误码" min-width="180" />
        <el-table-column prop="createdAtUtc" label="创建时间" min-width="210" />
      </el-table>
    </section>
  </div>
</template>

<style scoped>.jobs-page { display: grid; gap: 18px; }</style>
