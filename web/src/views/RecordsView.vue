<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { RefreshRight } from '@element-plus/icons-vue'
import { useRoute } from 'vue-router'
import { ApiError, apiRequest } from '@/services/apiClient'

type RecordValue = Record<string, unknown>
const route = useRoute()
const records = ref<RecordValue[]>([])
const loading = ref(false)
const error = ref('')
const traceId = ref('')
const title = computed(() => route.meta.title || '记录')
const columns = computed(() => {
  const keys = new Set<string>()
  for (const record of records.value.slice(0, 20)) Object.keys(record).forEach((key) => keys.add(key))
  return [...keys].filter((key) => !['detailJson', 'parametersJson', 'valueJson'].includes(key)).slice(0, 8)
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
  try { records.value = await apiRequest<RecordValue[]>(route.meta.endpoint) }
  catch (reason) {
    error.value = reason instanceof Error ? reason.message : '读取数据失败'
    traceId.value = reason instanceof ApiError ? reason.problem.traceId || '' : ''
  } finally { loading.value = false }
}

onMounted(load)
watch(() => route.fullPath, load)
</script>

<template>
  <div class="records-page">
    <header class="page-heading"><div><span class="panel__eyebrow">GOVERNANCE</span><h1>{{ title }}</h1><p>配置、状态和历史记录均来自后端持久化数据。</p></div><el-button :loading="loading" @click="load"><el-icon><RefreshRight /></el-icon>刷新</el-button></header>
    <el-alert v-if="error" :title="error" type="error" :description="traceId ? `追踪号：${traceId}` : undefined" show-icon :closable="false" />
    <section class="panel records-table" v-loading="loading">
      <el-empty v-if="!loading && !error && records.length === 0" description="暂无记录" />
      <el-table v-else :data="records" row-key="id">
        <el-table-column v-for="column in columns" :key="column" :label="column" min-width="150" show-overflow-tooltip>
          <template #default="scope">{{ display(scope.row[column]) }}</template>
        </el-table-column>
      </el-table>
    </section>
  </div>
</template>

<style scoped>.records-page { display: grid; gap: 18px; }.records-table { min-height: 260px; padding: 14px; }</style>
