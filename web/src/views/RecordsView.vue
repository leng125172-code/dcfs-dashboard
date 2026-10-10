<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { RefreshRight } from '@element-plus/icons-vue'
import { useRoute } from 'vue-router'
import { ApiError, apiRequest, apiUrl } from '@/services/apiClient'

type RecordValue = Record<string, unknown>
const route = useRoute()
const records = ref<RecordValue[]>([])
const loading = ref(false)
const error = ref('')
const traceId = ref('')
const actorSubject = ref('')
const action = ref('')
const result = ref('')
const title = computed(() => route.meta.title || '记录')
const isAudit = computed(() => route.name === 'audit')
const columns = computed(() => {
  const keys = new Set<string>()
  for (const record of records.value.slice(0, 20))
    Object.keys(record).forEach((key) => keys.add(key))
  return [...keys]
    .filter((key) => !['detailJson', 'parametersJson', 'valueJson'].includes(key))
    .slice(0, 8)
})

function display(value: unknown) {
  if (value === null || value === undefined || value === '') return '—'
  if (typeof value === 'boolean') return value ? '是' : '否'
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}

async function load() {
  if (!route.meta.endpoint) return
  loading.value = true
  error.value = ''
  try {
    const endpoint = isAudit.value ? auditEndpoint('audit', 500) : String(route.meta.endpoint)
    records.value = await apiRequest<RecordValue[]>(endpoint)
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : '读取数据失败'
    traceId.value = reason instanceof ApiError ? reason.problem.traceId || '' : ''
  } finally {
    loading.value = false
  }
}

function auditEndpoint(path: string, take: number) {
  const query = new URLSearchParams({ take: String(take) })
  if (actorSubject.value.trim()) query.set('actorSubject', actorSubject.value.trim())
  if (action.value.trim()) query.set('action', action.value.trim())
  if (result.value) query.set('result', result.value)
  return `${path}?${query.toString()}`
}

function exportAudit() {
  window.location.assign(apiUrl(auditEndpoint('audit/export', 5000)))
}

onMounted(load)
watch(() => route.fullPath, load)
</script>

<template>
  <div class="records-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">GOVERNANCE</span>
        <h1>{{ title }}</h1>
        <p>配置、状态和历史记录均来自后端持久化数据。</p>
      </div>
      <div class="page-heading__actions">
        <el-button v-if="isAudit" @click="exportAudit">导出 CSV</el-button>
        <el-button :loading="loading" @click="load"
          ><el-icon><RefreshRight /></el-icon>刷新</el-button
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
    <section v-if="isAudit" class="panel audit-filters">
      <el-input v-model="actorSubject" clearable placeholder="操作者 Subject" @keyup.enter="load" />
      <el-input
        v-model="action"
        clearable
        placeholder="动作，例如 containers.restart"
        @keyup.enter="load"
      />
      <el-select v-model="result" clearable placeholder="全部结果" @change="load">
        <el-option label="Accepted" value="Accepted" />
        <el-option label="Succeeded" value="Succeeded" />
        <el-option label="Failed" value="Failed" />
        <el-option label="Rejected" value="Rejected" />
      </el-select>
      <el-button type="primary" @click="load">查询</el-button>
    </section>
    <section class="panel records-table" v-loading="loading">
      <el-empty v-if="!loading && !error && records.length === 0" description="暂无记录" />
      <el-table v-else :data="records" row-key="id">
        <el-table-column
          v-for="column in columns"
          :key="column"
          :label="column"
          min-width="150"
          show-overflow-tooltip
        >
          <template #default="scope">{{ display(scope.row[column]) }}</template>
        </el-table-column>
      </el-table>
    </section>
  </div>
</template>

<style scoped>
.records-page {
  display: grid;
  gap: 18px;
}
.records-table {
  min-height: 260px;
  padding: 14px;
}
.audit-filters {
  display: grid;
  grid-template-columns: minmax(180px, 1fr) minmax(220px, 1fr) 160px auto;
  gap: 12px;
  padding: 14px;
}
@media (max-width: 760px) {
  .audit-filters {
    grid-template-columns: 1fr;
  }
}
</style>
