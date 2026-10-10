<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { apiRequest } from '@/services/apiClient'
import { enqueueOperation } from '@/services/operations'
import type { BackupPolicy, ManagedResource } from '@/services/contracts'

const items = ref<BackupPolicy[]>([])
const databases = ref<ManagedResource[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const editing = ref<BackupPolicy | null>(null)
const form = reactive({
  instanceResourceId: '',
  isEnabled: true,
  scheduleExpression: '0 2 * * *',
  timezone: 'Asia/Shanghai',
  retentionCount: 14,
  retentionDays: 14,
  targetDirectoryId: 'database-platform.backup.hdd',
  compression: 'zstd',
  verifyAfterBackup: true,
  capacityWarningPercent: 80,
  capacityCriticalPercent: 90,
})
async function load() {
  loading.value = true
  try {
    ;[items.value, databases.value] = await Promise.all([
      apiRequest<BackupPolicy[]>('backups/policies'),
      apiRequest<ManagedResource[]>('databases'),
    ])
  } finally {
    loading.value = false
  }
}
function open(item?: BackupPolicy) {
  editing.value = item || null
  Object.assign(
    form,
    item
      ? { ...item }
      : {
          instanceResourceId: '',
          isEnabled: true,
          scheduleExpression: '0 2 * * *',
          timezone: 'Asia/Shanghai',
          retentionCount: 14,
          retentionDays: 14,
          targetDirectoryId: 'database-platform.backup.hdd',
          compression: 'zstd',
          verifyAfterBackup: true,
          capacityWarningPercent: 80,
          capacityCriticalPercent: 90,
        },
  )
  dialogVisible.value = true
}
async function save() {
  const payload = {
    id: editing.value?.id || null,
    ...form,
    expectedVersion: editing.value?.version || null,
  }
  await apiRequest<BackupPolicy>('backups/policies', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
  dialogVisible.value = false
  ElMessage.success('备份策略已保存')
  await load()
}
async function run(item: BackupPolicy) {
  const job = await enqueueOperation('backups', 'run', item.instanceResourceId, {}, false)
  ElMessage.success(`备份任务已提交：${job.id}`)
}
async function verify(item: BackupPolicy) {
  const job = await enqueueOperation('backups', 'verify', item.instanceResourceId, {}, false)
  ElMessage.success(`校验任务已提交：${job.id}`)
}
onMounted(load)
</script>
<template>
  <div class="backup-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">BACKUPS</span>
        <h1>备份策略</h1>
        <p>备份写入已批准的 HDD 目标；当前不提供恢复入口。</p>
      </div>
      <div class="page-heading__actions">
        <el-button :loading="loading" @click="load">刷新</el-button
        ><el-button type="primary" @click="open()">新增策略</el-button>
      </div>
    </header>
    <section class="panel">
      <el-table :data="items" row-key="id" v-loading="loading"
        ><el-table-column
          prop="instanceResourceId"
          label="数据库资源"
          min-width="250"
        /><el-table-column prop="scheduleExpression" label="计划" min-width="140" /><el-table-column
          label="保留"
          width="150"
          ><template #default="scope"
            >{{ scope.row.retentionCount }} 份 / {{ scope.row.retentionDays }} 天</template
          ></el-table-column
        ><el-table-column label="状态" width="100"
          ><template #default="scope"
            ><el-tag :type="scope.row.isEnabled ? 'success' : 'info'">{{
              scope.row.isEnabled ? '启用' : '停用'
            }}</el-tag></template
          ></el-table-column
        ><el-table-column label="操作" width="230"
          ><template #default="scope"
            ><el-button link @click="open(scope.row)">编辑</el-button
            ><el-button link type="primary" @click="run(scope.row)">立即备份</el-button
            ><el-button link @click="verify(scope.row)">校验</el-button></template
          ></el-table-column
        ></el-table
      >
    </section>
    <el-dialog
      v-model="dialogVisible"
      :title="editing ? '编辑备份策略' : '新增备份策略'"
      width="min(680px, calc(100vw - 32px))"
      ><el-form label-position="top"
        ><el-alert
          title="从数据库平台选择受管资源；策略不会接受任意脚本或路径。"
          type="info"
          :closable="false" />
        <div class="form-grid">
          <el-form-item label="数据库资源"
            ><el-select v-model="form.instanceResourceId" :disabled="!!editing" filterable
              ><el-option
                v-for="database in databases"
                :key="database.id"
                :label="database.name"
                :value="database.id" /></el-select></el-form-item
          ><el-form-item label="Cron 表达式"
            ><el-input v-model="form.scheduleExpression" /></el-form-item
          ><el-form-item label="时区"><el-input v-model="form.timezone" /></el-form-item
          ><el-form-item label="压缩"
            ><el-select v-model="form.compression"
              ><el-option label="Zstandard" value="zstd" /><el-option
                label="Gzip"
                value="gzip" /></el-select></el-form-item
          ><el-form-item label="保留份数"
            ><el-input-number v-model="form.retentionCount" :min="1" :max="365" /></el-form-item
          ><el-form-item label="保留天数"
            ><el-input-number v-model="form.retentionDays" :min="1" :max="3650" /></el-form-item
          ><el-form-item label="容量告警 %"
            ><el-input-number
              v-model="form.capacityWarningPercent"
              :min="1"
              :max="98" /></el-form-item
          ><el-form-item label="容量严重 %"
            ><el-input-number
              v-model="form.capacityCriticalPercent"
              :min="2"
              :max="99" /></el-form-item
          ><el-form-item label="启用"><el-switch v-model="form.isEnabled" /></el-form-item
          ><el-form-item label="备份后校验"
            ><el-switch v-model="form.verifyAfterBackup"
          /></el-form-item></div></el-form
      ><template #footer
        ><el-button @click="dialogVisible = false">取消</el-button
        ><el-button type="primary" @click="save">保存</el-button></template
      ></el-dialog
    >
  </div>
</template>
<style scoped>
.backup-page {
  display: grid;
  gap: 18px;
}
.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0 14px;
  margin-top: 16px;
}
@media (max-width: 680px) {
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
