<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { apiRequest } from '@/services/apiClient'
import type { ScheduledTask, ScheduledTaskRun } from '@/services/contracts'

const items = ref<ScheduledTask[]>([])
const runs = ref<ScheduledTaskRun[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const editing = ref<ScheduledTask | null>(null)
const form = reactive({
  taskType: 'Backup',
  name: '',
  scheduleKind: 'Cron',
  scheduleExpression: '0 2 * * *',
  timezone: 'Asia/Shanghai',
  parametersJson: '{}',
  concurrencyPolicy: 'Forbid',
  timeoutSeconds: 3600,
  isEnabled: true,
})
async function load() {
  loading.value = true
  try {
    ;[items.value, runs.value] = await Promise.all([
      apiRequest<ScheduledTask[]>('schedules'),
      apiRequest<ScheduledTaskRun[]>('schedules/runs?take=100'),
    ])
  } finally {
    loading.value = false
  }
}
function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString() : '—'
}
function open(item?: ScheduledTask) {
  editing.value = item || null
  Object.assign(
    form,
    item
      ? {
          taskType: item.taskType,
          name: item.name,
          scheduleKind: item.scheduleKind,
          scheduleExpression: item.scheduleExpression,
          timezone: item.timezone,
          parametersJson: item.parametersJson,
          concurrencyPolicy: item.concurrencyPolicy,
          timeoutSeconds: item.timeoutSeconds,
          isEnabled: item.isEnabled,
        }
      : {
          taskType: 'Backup',
          name: '',
          scheduleKind: 'Cron',
          scheduleExpression: '0 2 * * *',
          timezone: 'Asia/Shanghai',
          parametersJson: '{}',
          concurrencyPolicy: 'Forbid',
          timeoutSeconds: 3600,
          isEnabled: true,
        },
  )
  dialogVisible.value = true
}
async function save() {
  JSON.parse(form.parametersJson)
  const payload = {
    id: editing.value?.id || null,
    ...form,
    expectedVersion: editing.value?.version || null,
  }
  const path = editing.value ? `schedules/${editing.value.id}` : 'schedules'
  const method = editing.value ? 'PUT' : 'POST'
  await apiRequest<ScheduledTask>(path, { method, body: JSON.stringify(payload) })
  dialogVisible.value = false
  ElMessage.success('计划任务已保存')
  await load()
}
async function remove(item: ScheduledTask) {
  await ElMessageBox.confirm(`删除计划任务“${item.name}”？历史执行记录不会删除。`, '删除计划任务', {
    type: 'warning',
  })
  await apiRequest<void>(`schedules/${item.id}?version=${item.version}`, { method: 'DELETE' })
  ElMessage.success('计划任务已删除')
  await load()
}
async function runNow(item: ScheduledTask) {
  await ElMessageBox.confirm(
    `立即运行计划任务“${item.name}”？任务会进入统一队列，并遵守资源锁和超时设置。`,
    '立即运行',
    { type: 'warning', confirmButtonText: '运行', cancelButtonText: '取消' },
  )
  await apiRequest<ScheduledTask>(`schedules/${item.id}/run`, { method: 'POST', body: '{}' })
  ElMessage.success('已安排立即运行，任务将在下一个 6 秒调度周期进入队列')
  await load()
  window.setTimeout(() => void load(), 7000)
}
onMounted(load)
</script>
<template>
  <div class="schedule-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">SCHEDULER</span>
        <h1>计划任务</h1>
        <p>只允许预定义任务类型，不接受脚本或任意命令。</p>
      </div>
      <div class="page-heading__actions">
        <el-button :loading="loading" @click="load">刷新</el-button
        ><el-button type="primary" @click="open()">新增计划</el-button>
      </div>
    </header>
    <section class="panel">
      <el-table :data="items" row-key="id" v-loading="loading"
        ><el-table-column prop="name" label="名称" min-width="180" /><el-table-column
          prop="taskType"
          label="类型"
          width="160"
        /><el-table-column label="周期" min-width="230"
          ><template #default="scope"
            >{{ scope.row.scheduleKind }} · {{ scope.row.scheduleExpression }}</template
          ></el-table-column
        ><el-table-column prop="nextRunAtUtc" label="下次执行" min-width="210" /><el-table-column
          label="状态"
          width="100"
          ><template #default="scope"
            ><el-tag :type="scope.row.isEnabled ? 'success' : 'info'">{{
              scope.row.isEnabled ? '启用' : '停用'
            }}</el-tag></template
          ></el-table-column
        ><el-table-column label="操作" width="200"
          ><template #default="scope"
            ><el-button
              link
              type="primary"
              :disabled="!scope.row.isEnabled"
              @click="runNow(scope.row)"
              >立即运行</el-button
            ><el-button link @click="open(scope.row)">编辑</el-button
            ><el-button link type="danger" @click="remove(scope.row)">删除</el-button></template
          ></el-table-column
        ></el-table
      >
    </section>
    <section class="panel run-history">
      <div class="panel-heading">
        <div><span>执行历史</span><small>最近 100 次计划执行</small></div>
      </div>
      <el-empty v-if="!loading && runs.length === 0" description="暂无执行记录" />
      <el-table v-else :data="runs" row-key="id" v-loading="loading">
        <el-table-column label="计划" min-width="180">
          <template #default="scope">{{
            items.find((item) => item.id === scope.row.scheduleId)?.name || scope.row.scheduleId
          }}</template>
        </el-table-column>
        <el-table-column label="计划时间" min-width="190"
          ><template #default="scope">{{
            formatDate(scope.row.scheduledForUtc)
          }}</template></el-table-column
        >
        <el-table-column prop="result" label="结果" width="120" />
        <el-table-column label="开始" min-width="190"
          ><template #default="scope">{{
            formatDate(scope.row.startedAtUtc)
          }}</template></el-table-column
        >
        <el-table-column label="完成" min-width="190"
          ><template #default="scope">{{
            formatDate(scope.row.completedAtUtc)
          }}</template></el-table-column
        >
        <el-table-column prop="jobId" label="任务 ID" min-width="260" show-overflow-tooltip />
      </el-table>
    </section>
    <el-dialog
      v-model="dialogVisible"
      :title="editing ? '编辑计划任务' : '新增计划任务'"
      width="min(640px, calc(100vw - 32px))"
      ><el-form label-position="top"
        ><div class="form-grid">
          <el-form-item label="任务类型"
            ><el-select v-model="form.taskType"
              ><el-option label="数据库备份" value="Backup" /><el-option
                label="应用更新"
                value="ApplicationUpdate" /><el-option
                label="独立容器更新"
                value="ContainerUpdate" /><el-option
                label="系统更新"
                value="SystemUpdate" /><el-option
                label="指标聚合"
                value="MetricsRollup" /><el-option
                label="保留清理"
                value="RetentionCleanup" /></el-select></el-form-item
          ><el-form-item label="名称"><el-input v-model="form.name" /></el-form-item
          ><el-form-item label="周期类型"
            ><el-select v-model="form.scheduleKind"
              ><el-option label="Cron" value="Cron" /><el-option
                label="每天"
                value="Daily" /><el-option
                label="固定间隔"
                value="Interval" /></el-select></el-form-item
          ><el-form-item label="周期表达式"
            ><el-input v-model="form.scheduleExpression" /></el-form-item
          ><el-form-item label="时区"><el-input v-model="form.timezone" /></el-form-item
          ><el-form-item label="并发策略"
            ><el-select v-model="form.concurrencyPolicy"
              ><el-option label="禁止重叠" value="Forbid" /><el-option
                label="替换运行中的任务"
                value="Replace" /><el-option
                label="允许并行"
                value="Allow" /></el-select></el-form-item
          ><el-form-item label="超时秒数"
            ><el-input-number v-model="form.timeoutSeconds" :min="30" :max="86400" /></el-form-item
          ><el-form-item label="启用"><el-switch v-model="form.isEnabled" /></el-form-item>
        </div>
        <el-form-item label="类型化参数 JSON"
          ><el-input
            v-model="form.parametersJson"
            type="textarea"
            :rows="4" /></el-form-item></el-form
      ><template #footer
        ><el-button @click="dialogVisible = false">取消</el-button
        ><el-button type="primary" @click="save">保存</el-button></template
      ></el-dialog
    >
  </div>
</template>
<style scoped>
.schedule-page {
  display: grid;
  gap: 18px;
}
.run-history {
  padding: 14px;
}
.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0 14px;
}
@media (max-width: 680px) {
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
