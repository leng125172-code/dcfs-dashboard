<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { apiRequest } from '@/services/apiClient'
import { subscribeToJob } from '@/services/operations'
import type { Job, JobEvent } from '@/services/contracts'

const jobs = ref<Job[]>([])
const loading = ref(false)
const selected = ref<Job | null>(null)
const events = ref<JobEvent[]>([])
const drawerVisible = ref(false)
let timer: number | undefined
let unsubscribe: (() => void) | undefined
const terminalStates = new Set(['Succeeded', 'Failed', 'Canceled', 'RolledBack'])
const canCancel = computed(() => selected.value && !terminalStates.has(selected.value.state))

function stateType(state: string) {
  if (state === 'Succeeded') return 'success'
  if (state === 'Failed') return 'danger'
  if (state === 'Canceled' || state === 'RolledBack') return 'warning'
  return 'primary'
}

async function load() {
  loading.value = true
  try {
    jobs.value = await apiRequest<Job[]>('jobs?take=100')
    if (selected.value)
      selected.value = jobs.value.find((item) => item.id === selected.value?.id) ?? selected.value
  } finally {
    loading.value = false
  }
}

async function openJob(job: Job) {
  unsubscribe?.()
  events.value = []
  selected.value = await apiRequest<Job>(`jobs/${job.id}`)
  drawerVisible.value = true
  if (!terminalStates.has(selected.value.state)) {
    unsubscribe = subscribeToJob(job.id, {
      progress(event) {
        events.value.push(event)
        if (selected.value) {
          selected.value.state = event.state
          selected.value.phase = event.phase
          selected.value.progressPercent = event.progressPercent
        }
      },
      error: () => void load(),
    })
  }
}

async function cancelJob() {
  if (!selected.value) return
  await ElMessageBox.confirm(
    `请求取消任务 ${selected.value.id}？正在执行的安全阶段可能需要等待。`,
    '取消任务',
    { type: 'warning' },
  )
  await apiRequest<void>(`jobs/${selected.value.id}/cancel`, { method: 'POST' })
  ElMessage.success('已提交取消请求')
  await load()
}

onMounted(() => {
  void load()
  timer = window.setInterval(load, 6000)
})
onBeforeUnmount(() => {
  window.clearInterval(timer)
  unsubscribe?.()
})
</script>

<template>
  <div class="jobs-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">OPERATIONS</span>
        <h1>任务中心</h1>
        <p>查看异步任务的实时阶段、结果和错误；任务进度使用 SSE 持续更新。</p>
      </div>
      <el-button :loading="loading" @click="load">刷新</el-button>
    </header>
    <section class="panel" v-loading="loading">
      <el-table :data="jobs" row-key="id" empty-text="暂无任务" @row-click="openJob">
        <el-table-column prop="jobType" label="任务" min-width="210" />
        <el-table-column label="状态" width="130"
          ><template #default="scope"
            ><el-tag :type="stateType(scope.row.state)" effect="light">{{
              scope.row.state
            }}</el-tag></template
          ></el-table-column
        >
        <el-table-column prop="phase" label="阶段" min-width="180" />
        <el-table-column label="进度" width="180"
          ><template #default="scope"
            ><el-progress
              :percentage="scope.row.progressPercent ?? 0"
              :status="scope.row.state === 'Failed' ? 'exception' : undefined" /></template
        ></el-table-column>
        <el-table-column prop="errorCode" label="错误码" min-width="180" />
        <el-table-column prop="createdAtUtc" label="创建时间" min-width="210" />
        <el-table-column label="操作" width="90" fixed="right"
          ><template #default="scope"
            ><el-button link type="primary" @click.stop="openJob(scope.row)"
              >详情</el-button
            ></template
          ></el-table-column
        >
      </el-table>
    </section>

    <el-drawer
      v-model="drawerVisible"
      title="任务详情"
      size="min(560px, 94vw)"
      destroy-on-close
      @closed="unsubscribe?.()"
    >
      <template v-if="selected">
        <el-descriptions :column="1" border>
          <el-descriptions-item label="任务 ID">{{ selected.id }}</el-descriptions-item>
          <el-descriptions-item label="类型">{{ selected.jobType }}</el-descriptions-item>
          <el-descriptions-item label="状态"
            ><el-tag :type="stateType(selected.state)">{{
              selected.state
            }}</el-tag></el-descriptions-item
          >
          <el-descriptions-item label="阶段">{{ selected.phase }}</el-descriptions-item>
          <el-descriptions-item label="错误码">{{
            selected.errorCode || '—'
          }}</el-descriptions-item>
          <el-descriptions-item label="结果">
            <pre class="job-result">{{ selected.resultJson || '任务成功后显示安全结果。' }}</pre>
          </el-descriptions-item>
        </el-descriptions>
        <h3 class="job-events-title">实时事件</h3>
        <el-timeline>
          <el-timeline-item
            v-for="event in events"
            :key="event.sequence"
            :timestamp="event.occurredAtUtc"
            placement="top"
            ><strong>{{ event.phase }}</strong>
            <div>{{ event.messageCode }} · {{ event.progressPercent ?? 0 }}%</div></el-timeline-item
          >
        </el-timeline>
      </template>
      <template #footer
        ><el-button v-if="canCancel" type="danger" plain @click="cancelJob"
          >取消任务</el-button
        ></template
      >
    </el-drawer>
  </div>
</template>

<style scoped>
.jobs-page {
  display: grid;
  gap: 18px;
}
.job-result {
  overflow-wrap: anywhere;
  margin: 0;
  white-space: pre-wrap;
}
.job-events-title {
  margin: 24px 0 16px;
  font-size: 16px;
}
</style>
