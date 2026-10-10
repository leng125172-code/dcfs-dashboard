<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { RefreshRight } from '@element-plus/icons-vue'
import { useRoute } from 'vue-router'
import { ApiError, apiRequest } from '@/services/apiClient'
import type { ManagedResource } from '@/services/contracts'

const route = useRoute()
const loading = ref(false)
const error = ref('')
const traceId = ref('')
const resources = ref<ManagedResource[]>([])
const title = computed(() => route.meta.title || '资源')

async function load() {
  const endpoint = route.meta.endpoint
  if (!endpoint) return
  loading.value = true
  error.value = ''
  try {
    const value = await apiRequest<ManagedResource[] | { applications: ManagedResource[] }>(endpoint)
    resources.value = Array.isArray(value) ? value : value.applications
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : '读取资源失败'
    traceId.value = reason instanceof ApiError ? reason.problem.traceId || '' : ''
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(() => route.fullPath, load)
</script>

<template>
  <div class="resource-page">
    <header class="page-heading">
      <div><span class="panel__eyebrow">MANAGEMENT</span><h1>{{ title }}</h1><p>读取工作站上的真实资源状态；管理动作只对管理员开放。</p></div>
      <el-button :loading="loading" @click="load"><el-icon><RefreshRight /></el-icon>刷新</el-button>
    </header>
    <el-alert v-if="error" :title="error" type="error" :description="traceId ? `追踪号：${traceId}` : undefined" show-icon :closable="false" />
    <section class="panel resource-table-panel" v-loading="loading">
      <el-empty v-if="!loading && !error && resources.length === 0" description="没有可显示的资源" />
      <el-table v-else :data="resources" row-key="id">
        <el-table-column prop="name" label="名称" min-width="220" />
        <el-table-column prop="type" label="类型" min-width="150" />
        <el-table-column prop="state" label="状态" min-width="120"><template #default="scope"><el-tag effect="plain">{{ scope.row.state }}</el-tag></template></el-table-column>
        <el-table-column prop="version" label="版本 / 镜像" min-width="220" show-overflow-tooltip />
        <el-table-column label="保护" width="110"><template #default="scope"><el-tag :type="scope.row.isProtected ? 'warning' : 'info'" effect="light">{{ scope.row.isProtected ? '受保护' : '普通' }}</el-tag></template></el-table-column>
      </el-table>
    </section>
  </div>
</template>

<style scoped>
.resource-page { display: grid; gap: 18px; }
.resource-table-panel { min-height: 280px; padding: 14px; }
</style>
