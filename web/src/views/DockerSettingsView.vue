<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { enqueueOperation, planOperation } from '@/services/operations'

const settingsJson = ref(
  JSON.stringify(
    {
      'registry-mirrors': ['https://docker.xuanyuan.cloud'],
      'log-driver': 'local',
      'log-opts': { 'max-size': '10m', 'max-file': '5', compress: 'true' },
      'live-restore': true,
    },
    null,
    2,
  ),
)
const submitting = ref(false)
function parse() {
  const value = JSON.parse(settingsJson.value) as unknown
  if (!value || Array.isArray(value) || typeof value !== 'object')
    throw new Error('配置必须是 JSON 对象')
  return settingsJson.value
}
async function validate() {
  submitting.value = true
  try {
    const job = await enqueueOperation(
      'docker',
      'validate-settings',
      null,
      { settingsJson: parse() },
      false,
    )
    ElMessage.success(`校验任务已提交：${job.id}`)
  } finally {
    submitting.value = false
  }
}
async function apply() {
  submitting.value = true
  try {
    const parameters = { settingsJson: parse() }
    const plan = await planOperation('docker', 'apply-settings', null, parameters)
    await ElMessageBox.confirm(
      [
        ...plan.changes,
        ...plan.warnings,
        '应用期间 Docker 和 Whale Deck 容器会短暂不可用，宿主机维护页将持续显示状态；失败会自动恢复旧配置。',
      ].join('\n'),
      '应用 Docker 设置',
      { type: 'warning', confirmButtonText: '确认重启 Docker' },
    )
    const job = await enqueueOperation(
      'docker',
      'apply-settings',
      null,
      parameters,
      true,
      plan.planHash,
    )
    ElMessage.success(`维护任务已提交：${job.id}`)
  } finally {
    submitting.value = false
  }
}
</script>
<template>
  <div class="docker-settings-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">DOCKER ENGINE</span>
        <h1>Docker 设置</h1>
        <p>仅允许受控字段；后端合并未知字段、原子备份并在重启失败时自动回滚。</p>
      </div>
    </header>
    <el-alert
      title="保存会重启 Docker。进行中请停留在维护加载页，直到 API 与核心依赖恢复健康。"
      type="warning"
      show-icon
      :closable="false"
    />
    <section class="panel editor-panel">
      <el-form label-position="top"
        ><el-form-item label="允许修改的 daemon.json 片段"
          ><el-input
            v-model="settingsJson"
            type="textarea"
            :rows="18"
            spellcheck="false" /></el-form-item
      ></el-form>
      <div class="editor-actions">
        <el-button :loading="submitting" @click="validate">只校验</el-button
        ><el-button type="primary" :loading="submitting" @click="apply">预检、保存并重启</el-button>
      </div>
    </section>
  </div>
</template>
<style scoped>
.docker-settings-page {
  display: grid;
  gap: 18px;
}
.editor-panel {
  padding: 20px;
}
.editor-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}
.editor-panel :deep(textarea) {
  font-family: var(--whaledeck-font-family);
  line-height: 1.6;
}
</style>
