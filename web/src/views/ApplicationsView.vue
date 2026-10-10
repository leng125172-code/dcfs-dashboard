<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { apiRequest } from '@/services/apiClient'
import { enqueueOperation, planOperation, stageSecret } from '@/services/operations'
import type { CatalogApplication, ManagedResource } from '@/services/contracts'

const catalog = ref<CatalogApplication[]>([])
const installed = ref<ManagedResource[]>([])
const stale = ref(false)
const loading = ref(false)
const installVisible = ref(false)
const submitting = ref(false)
const form = reactive({ slug: '', image: '', ports: '', environment: '', autoupdate: false })

async function load() {
  loading.value = true
  try {
    const [popular, apps] = await Promise.all([
      apiRequest<{ applications: CatalogApplication[]; isStale: boolean }>('applications/popular'),
      apiRequest<ManagedResource[]>('applications'),
    ])
    catalog.value = popular.applications
    stale.value = popular.isStale
    installed.value = apps
  } finally {
    loading.value = false
  }
}

function openInstall(item?: CatalogApplication) {
  Object.assign(form, {
    slug: item?.id || '',
    image: item?.image || '',
    ports: '',
    environment: '',
    autoupdate: false,
  })
  installVisible.value = true
}

async function install() {
  submitting.value = true
  try {
    const parameters: Record<string, string> = { slug: form.slug.trim(), image: form.image.trim() }
    parameters.autoupdate = String(form.autoupdate)
    if (form.ports.trim()) parameters.ports = form.ports.trim()
    if (form.environment.trim()) {
      const ticket = await stageSecret(form.environment)
      parameters.inputTicket = ticket.token
      parameters.inputKind = 'environment'
    }
    const plan = await planOperation('applications', 'install', '', parameters)
    await ElMessageBox.confirm(
      [...plan.changes, ...plan.warnings].join('\n') || '将生成受控 Compose 配置并启动应用。',
      '安装确认',
      { type: 'warning', confirmButtonText: '安装' },
    )
    const job = await enqueueOperation(
      'applications',
      'install',
      '',
      parameters,
      true,
      plan.planHash,
    )
    installVisible.value = false
    ElMessage.success(`安装任务已提交：${job.id}`)
  } finally {
    submitting.value = false
  }
}

async function lifecycle(
  item: ManagedResource,
  action: 'start' | 'stop' | 'restart' | 'update' | 'reinstall' | 'uninstall',
) {
  const slug = item.id.replace(/^application:/, '')
  const parameters = { slug, deleteVolumes: 'false' }
  let planHash: string | null = null
  if (['update', 'reinstall', 'uninstall'].includes(action)) {
    const plan = await planOperation('applications', action, '', parameters)
    planHash = plan.planHash
    await ElMessageBox.confirm(
      [...plan.changes, ...plan.warnings].join('\n') || `${action} ${slug}，卸载默认保留数据卷。`,
      '应用操作确认',
      { type: action === 'uninstall' ? 'error' : 'warning' },
    )
  } else if (action !== 'start') {
    await ElMessageBox.confirm(
      `确认${action === 'stop' ? '停止' : '重启'} ${slug}？`,
      '应用操作确认',
      { type: 'warning' },
    )
  }
  const job = await enqueueOperation('applications', action, '', parameters, true, planHash)
  ElMessage.success(`任务已提交：${job.id}`)
}

onMounted(load)
</script>

<template>
  <div class="applications-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">APPLICATIONS</span>
        <h1>应用管理</h1>
        <p>安装模板来自轩辕热门目录；应用写入独立目录并受 Whale Deck 管理。</p>
      </div>
      <div class="page-heading__actions">
        <el-button :loading="loading" @click="load">刷新</el-button
        ><el-button type="primary" @click="openInstall()">自定义安装</el-button>
      </div>
    </header>
    <el-alert
      v-if="stale"
      title="热门目录当前使用最近一次有效快照"
      type="warning"
      show-icon
      :closable="false"
    />
    <section class="panel app-section">
      <div class="panel__header">
        <div>
          <span class="panel__eyebrow">XUANYUAN</span>
          <h2>热门应用</h2>
        </div>
      </div>
      <div class="catalog-grid">
        <article v-for="item in catalog" :key="item.id" class="catalog-card">
          <div class="catalog-card__icon">
            <img v-if="item.iconUrl" :src="item.iconUrl" alt="" /><span v-else>{{
              item.name.slice(0, 1)
            }}</span>
          </div>
          <div>
            <strong>{{ item.name }}</strong>
            <p>{{ item.description }}</p>
            <small>{{ item.version }} · {{ item.image }}</small>
          </div>
          <el-button type="primary" plain :disabled="item.installed" @click="openInstall(item)">{{
            item.installed ? '已安装' : '安装'
          }}</el-button>
        </article>
      </div>
    </section>
    <section class="panel app-section">
      <div class="panel__header">
        <div>
          <span class="panel__eyebrow">INSTALLED</span>
          <h2>已安装应用</h2>
        </div>
      </div>
      <el-empty v-if="!loading && installed.length === 0" description="暂无已安装应用" />
      <div v-else class="installed-list">
        <article v-for="item in installed" :key="item.id" class="installed-row">
          <div>
            <strong>{{ item.name }}</strong
            ><small>{{ item.version }}</small>
          </div>
          <el-tag>{{ item.state }}</el-tag>
          <div class="installed-row__actions">
            <el-button link @click="lifecycle(item, 'start')">启动</el-button
            ><el-button link type="danger" @click="lifecycle(item, 'stop')">停止</el-button
            ><el-button link @click="lifecycle(item, 'restart')">重启</el-button
            ><el-button link @click="lifecycle(item, 'update')">更新</el-button
            ><el-button link @click="lifecycle(item, 'reinstall')">重装</el-button
            ><el-button link type="danger" @click="lifecycle(item, 'uninstall')">卸载</el-button>
          </div>
        </article>
      </div>
    </section>

    <el-dialog v-model="installVisible" title="安装应用" width="min(640px, calc(100vw - 32px))">
      <el-form label-position="top"
        ><el-form-item label="应用标识"
          ><el-input v-model="form.slug" placeholder="my-app" /></el-form-item
        ><el-form-item label="OCI 镜像"><el-input v-model="form.image" /></el-form-item
        ><el-form-item label="端口映射"
          ><el-input v-model="form.ports" placeholder="192.168.100.13:8088:80/tcp" /><small
            class="form-tip"
            >多个映射用逗号分隔，仅允许工作站批准的两个地址或回环地址。</small
          ></el-form-item
        ><el-form-item label="环境变量（可选）"
          ><el-input
            v-model="form.environment"
            type="textarea"
            :rows="5"
            placeholder="KEY=value"
          /><small class="form-tip"
            >内容经一次性 Secret 传输，不写入任务或审计。</small
          ></el-form-item
        ><el-form-item label="自动更新"
          ><el-switch v-model="form.autoupdate" /><small class="form-tip"
            >只有启用后写入 autoupdate 标签，计划任务才允许更新此应用。</small
          ></el-form-item
        ></el-form
      >
      <template #footer
        ><el-button @click="installVisible = false">取消</el-button
        ><el-button
          type="primary"
          :loading="submitting"
          :disabled="!form.slug || !form.image"
          @click="install"
          >预检并安装</el-button
        ></template
      >
    </el-dialog>
  </div>
</template>

<style scoped>
.applications-page {
  display: grid;
  gap: 18px;
}
.app-section {
  overflow: hidden;
}
.catalog-grid,
.installed-list {
  display: grid;
}
.catalog-card,
.installed-row {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 14px;
  padding: 14px 18px;
  border-bottom: 1px solid var(--el-border-color-extra-light);
}
.catalog-card:last-child,
.installed-row:last-child {
  border-bottom: 0;
}
.catalog-card__icon {
  display: grid;
  width: 46px;
  height: 46px;
  place-items: center;
  border-radius: 10px;
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
  font-weight: 700;
}
.catalog-card__icon img {
  width: 34px;
  height: 34px;
  object-fit: contain;
}
.catalog-card p {
  margin: 3px 0;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
.catalog-card small,
.installed-row small,
.form-tip {
  display: block;
  color: var(--el-text-color-placeholder);
  font-size: 11px;
}
.installed-row > div:first-child {
  display: grid;
  gap: 5px;
}
.installed-row__actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
}
@media (max-width: 760px) {
  .catalog-card,
  .installed-row {
    grid-template-columns: auto minmax(0, 1fr);
  }
  .catalog-card > .el-button,
  .installed-row__actions {
    grid-column: 2;
  }
}
</style>
