<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Check, RefreshRight } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { ApiError, apiRequest } from '@/services/apiClient'
import type { PlatformSetting } from '@/services/contracts'

interface SettingDefinition {
  key: string
  title: string
  description: string
  defaultValue: object
}
interface SettingEditor extends SettingDefinition {
  valueJson: string
  version: number | null
  updatedAtUtc: string | null
  saving: boolean
}

const definitions: SettingDefinition[] = [
  {
    key: 'maintenance.window',
    title: '维护窗口',
    description: '系统更新和应用自动更新允许执行的时间窗口。',
    defaultValue: {
      timezone: 'Asia/Shanghai',
      days: ['Sunday'],
      start: '20:00',
      durationMinutes: 120,
    },
  },
  {
    key: 'applications.autoupdate',
    title: '应用自动更新',
    description: '仅处理带 autoupdate=true 标签的普通应用。',
    defaultValue: { enabled: false, requireLabel: 'autoupdate=true' },
  },
  {
    key: 'metrics.retention',
    title: '指标采集与保留',
    description: '采集间隔固定为 6 秒，原始和聚合指标默认保留 14 天。',
    defaultValue: { sampleSeconds: 6, rawDays: 14, rollupDays: 14 },
  },
  {
    key: 'catalog.refresh',
    title: '轩辕目录刷新',
    description: '热门应用目录的刷新和过期判定周期。',
    defaultValue: { intervalMinutes: 60, staleAfterMinutes: 180 },
  },
  {
    key: 'ui.branding',
    title: '界面品牌',
    description: 'Whale Deck 的产品名称与主题强调色。',
    defaultValue: { productName: 'Whale Deck', accentColor: '#409eff' },
  },
]
const editors = ref<SettingEditor[]>([])
const loading = ref(false)
const error = ref('')
const traceId = ref('')

function pretty(value: string) {
  return JSON.stringify(JSON.parse(value), null, 2)
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const values = await apiRequest<PlatformSetting[]>('settings')
    const byKey = new Map(values.map((item) => [item.key, item]))
    editors.value = definitions.map((definition) => {
      const value = byKey.get(definition.key)
      return {
        ...definition,
        valueJson: value
          ? pretty(value.valueJson)
          : JSON.stringify(definition.defaultValue, null, 2),
        version: value?.version ?? null,
        updatedAtUtc: value?.updatedAtUtc ?? null,
        saving: false,
      }
    })
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : '读取平台设置失败'
    traceId.value = reason instanceof ApiError ? reason.problem.traceId || '' : ''
  } finally {
    loading.value = false
  }
}

async function save(item: SettingEditor) {
  const normalized = JSON.stringify(JSON.parse(item.valueJson))
  item.saving = true
  try {
    const saved = await apiRequest<PlatformSetting>(`settings/${encodeURIComponent(item.key)}`, {
      method: 'PUT',
      body: JSON.stringify({ valueJson: normalized, expectedVersion: item.version }),
    })
    item.valueJson = pretty(saved.valueJson)
    item.version = saved.version
    item.updatedAtUtc = saved.updatedAtUtc
    ElMessage.success(`${item.title}已保存`)
  } finally {
    item.saving = false
  }
}

onMounted(load)
</script>

<template>
  <div class="settings-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">PLATFORM</span>
        <h1>平台设置</h1>
        <p>这里只开放经过后端白名单校验的设置项；保存时使用版本号避免覆盖他人的修改。</p>
      </div>
      <el-button :loading="loading" @click="load"
        ><el-icon><RefreshRight /></el-icon>重新载入</el-button
      >
    </header>
    <el-alert
      v-if="error"
      :title="error"
      type="error"
      :description="traceId ? `追踪号：${traceId}` : undefined"
      show-icon
      :closable="false"
    />
    <section class="settings-list" v-loading="loading">
      <article v-for="item in editors" :key="item.key" class="panel setting-card">
        <header>
          <div>
            <h2>{{ item.title }}</h2>
            <code>{{ item.key }}</code>
          </div>
          <span>{{
            item.updatedAtUtc
              ? `更新于 ${new Date(item.updatedAtUtc).toLocaleString()}`
              : '尚未保存，当前显示默认值'
          }}</span>
        </header>
        <p>{{ item.description }}</p>
        <el-input
          v-model="item.valueJson"
          type="textarea"
          :rows="7"
          resize="vertical"
          spellcheck="false"
        />
        <footer>
          <span>版本 {{ item.version ?? '新建' }}</span
          ><el-button type="primary" :loading="item.saving" @click="save(item)"
            ><el-icon><Check /></el-icon>保存</el-button
          >
        </footer>
      </article>
    </section>
  </div>
</template>

<style scoped>
.settings-page {
  display: grid;
  gap: 18px;
}
.settings-list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 14px;
}
.setting-card {
  display: grid;
  gap: 12px;
  padding: 18px;
}
.setting-card header,
.setting-card footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}
.setting-card h2 {
  margin: 0;
  font-size: 16px;
}
.setting-card code,
.setting-card header > span,
.setting-card footer > span {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
.setting-card p {
  margin: 0;
  color: var(--el-text-color-regular);
  font-size: 13px;
}
@media (max-width: 960px) {
  .settings-list {
    grid-template-columns: 1fr;
  }
}
@media (max-width: 560px) {
  .setting-card header {
    align-items: flex-start;
    flex-direction: column;
    gap: 6px;
  }
}
</style>
