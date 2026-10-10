<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { apiRequest } from '@/services/apiClient'
import type { ManagedResource } from '@/services/contracts'
import { enqueueOperation, planOperation } from '@/services/operations'
const message = ref('chore: save Whale Deck configuration')
const running = ref(false)
const status = ref<ManagedResource | null>(null)
const statusLoading = ref(false)
async function refreshStatus() {
  statusLoading.value = true
  try {
    status.value = await apiRequest<ManagedResource>('config-repository/status')
  } finally {
    statusLoading.value = false
  }
}
async function snapshot() {
  running.value = true
  try {
    const job = await enqueueOperation('config-repository', 'snapshot', null, {}, false)
    ElMessage.success(`敏感扫描任务已提交：${job.id}`)
  } finally {
    running.value = false
  }
}
async function commit() {
  running.value = true
  try {
    const parameters = { message: message.value.trim() }
    const plan = await planOperation('config-repository', 'commit-push', null, parameters)
    await ElMessageBox.confirm(
      [
        ...plan.changes,
        ...plan.warnings,
        '提交前会再次扫描敏感文件名和敏感内容；任何命中都会阻止推送。',
      ].join('\n'),
      '提交并推送配置',
      { type: 'warning' },
    )
    const job = await enqueueOperation(
      'config-repository',
      'commit-push',
      null,
      parameters,
      true,
      plan.planHash,
    )
    ElMessage.success(`配置同步任务已提交：${job.id}`)
  } finally {
    running.value = false
  }
}
onMounted(refreshStatus)
</script>
<template>
  <div class="config-repo-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">CONFIGURATION</span>
        <h1>配置仓库</h1>
        <p>仅操作宿主机注册表批准的 database-platform 仓库，不接受任意路径或远程地址。</p>
      </div>
    </header>
    <section class="panel repo-panel">
      <el-descriptions v-loading="statusLoading" :column="3" border size="small">
        <el-descriptions-item label="状态">
          <el-tag :type="status?.state === 'Clean' ? 'success' : 'warning'">
            {{ status?.state ?? '读取中' }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="分支">{{
          status?.attributes.branch ?? '—'
        }}</el-descriptions-item>
        <el-descriptions-item label="提交">{{
          status?.version?.slice(0, 12) ?? '—'
        }}</el-descriptions-item>
      </el-descriptions>
      <el-alert
        title=".env、Token、密码、私钥和疑似敏感内容会在提交前被阻止。"
        type="info"
        show-icon
        :closable="false"
      />
      <div class="repo-actions">
        <article>
          <h2>快照与敏感扫描</h2>
          <p>扫描已跟踪及待提交文件，确认仓库可安全保存。</p>
          <el-button :loading="running" @click="snapshot">创建安全快照</el-button>
        </article>
        <article>
          <h2>提交并推送</h2>
          <p>使用固定身份提交到当前分支并推送 origin。</p>
          <el-input v-model="message" maxlength="120" show-word-limit /><el-button
            type="primary"
            :disabled="message.trim().length < 3"
            :loading="running"
            @click="commit"
            >预检并推送</el-button
          >
        </article>
      </div>
    </section>
  </div>
</template>
<style scoped>
.config-repo-page {
  display: grid;
  gap: 18px;
}
.repo-panel {
  padding: 20px;
}
.repo-actions {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  margin-top: 18px;
}
.repo-actions article {
  padding: 18px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 10px;
}
.repo-actions h2 {
  margin: 0;
  font-size: 16px;
}
.repo-actions p {
  min-height: 44px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  line-height: 1.7;
}
.repo-actions .el-button {
  margin-top: 12px;
}
@media (max-width: 720px) {
  .repo-actions {
    grid-template-columns: 1fr;
  }
}
</style>
