<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { apiRequest } from '@/services/apiClient'
import { enqueueOperation, planOperation, stageSecret } from '@/services/operations'
import type {
  ApplicationImageMetadata,
  ApplicationDetail,
  ApplicationInstallation,
  CatalogApplication,
  ManagedResource,
} from '@/services/contracts'

const catalog = ref<CatalogApplication[]>([])
const route = useRoute()
const router = useRouter()
const installed = ref<ManagedResource[]>([])
const installations = ref<ApplicationInstallation[]>([])
const detail = ref<ApplicationDetail | null>(null)
const detailVisible = ref(false)
const detailLoading = ref(false)
const stale = ref(false)
const loading = ref(false)
const installVisible = ref(false)
const submitting = ref(false)
const inspectingImage = ref(false)
const imageMetadata = ref<ApplicationImageMetadata | null>(null)
const form = reactive({
  slug: '',
  displayName: '',
  image: '',
  ports: '',
  environment: '',
  autoupdate: false,
  versionRange: '*',
  maintenanceWindow: 'Sun@20:00-23:59',
  dependencies: '',
})

async function load() {
  loading.value = true
  try {
    const [popular, apps, records] = await Promise.all([
      apiRequest<{ applications: CatalogApplication[]; isStale: boolean }>('applications/popular'),
      apiRequest<ManagedResource[]>('applications'),
      apiRequest<ApplicationInstallation[]>('applications/installations'),
    ])
    catalog.value = popular.applications
    stale.value = popular.isStale
    installed.value = apps
    installations.value = records
  } finally {
    loading.value = false
  }
}

function openInstall(item?: CatalogApplication) {
  Object.assign(form, {
    slug: item?.id || '',
    displayName: item?.name || '',
    image: item?.image || '',
    ports: '',
    environment: '',
    autoupdate: false,
    versionRange: '*',
    maintenanceWindow: 'Sun@20:00-23:59',
    dependencies: '',
  })
  imageMetadata.value = null
  installVisible.value = true
}

async function inspectImage() {
  if (!form.image.trim()) return
  inspectingImage.value = true
  try {
    imageMetadata.value = await apiRequest<ApplicationImageMetadata>(
      'applications/image-metadata',
      {
        method: 'POST',
        signal: AbortSignal.timeout(180_000),
        body: JSON.stringify({ image: form.image.trim(), pullIfMissing: true }),
      },
    )
    if (!form.environment.trim() && imageMetadata.value.environment.length) {
      form.environment = imageMetadata.value.environment.join('\n')
    }
    ElMessage.success('已读取 OCI 镜像声明')
  } finally {
    inspectingImage.value = false
  }
}

async function install() {
  submitting.value = true
  try {
    const parameters: Record<string, string> = { slug: form.slug.trim(), image: form.image.trim() }
    parameters.catalogAppId = form.slug.trim()
    parameters.templateId = itemTemplateId()
    parameters.templateVersion = '1'
    parameters.displayName = form.displayName.trim() || form.slug.trim()
    parameters.autoupdate = String(form.autoupdate)
    parameters.versionRange = form.versionRange.trim() || '*'
    parameters.maintenanceWindow = form.maintenanceWindow.trim()
    if (form.dependencies.trim()) parameters.dependencies = form.dependencies.trim()
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

function itemTemplateId() {
  return catalog.value.some((item) => item.id === form.slug.trim()) ? 'xuanyuan' : 'custom-oci'
}

function installationFor(item: ManagedResource) {
  const slug = item.id.replace(/^application:/, '')
  return installations.value.find((record) => record.catalogAppId === slug)
}

async function openDetail(id: string) {
  detailLoading.value = true
  detailVisible.value = true
  try {
    detail.value = await apiRequest<ApplicationDetail>(`applications/installations/${id}`)
    if (route.params.id !== id) await router.push({ name: 'application-detail', params: { id } })
  } finally {
    detailLoading.value = false
  }
}

async function syncDetailFromRoute() {
  const id = typeof route.params.id === 'string' ? route.params.id : ''
  if (id) await openDetail(id)
}

async function closeDetail() {
  detailVisible.value = false
  detail.value = null
  if (route.name === 'application-detail') await router.push({ name: 'applications' })
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

onMounted(async () => {
  await load()
  if (route.query.action === 'install' && typeof route.query.app === 'string') {
    const item = catalog.value.find((candidate) => candidate.id === route.query.app)
    if (item && !item.installed) openInstall(item)
  }
  await syncDetailFromRoute()
})

watch(
  () => route.params.id,
  async () => {
    if (route.name === 'application-detail') await syncDetailFromRoute()
  },
)
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
            <el-button
              v-if="installationFor(item)"
              link
              type="primary"
              @click="openDetail(installationFor(item)?.id || '')"
              >详情</el-button
            >
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
        ><el-form-item label="显示名称"
          ><el-input v-model="form.displayName" placeholder="My App" /></el-form-item
        ><el-form-item label="OCI 镜像"
          ><el-input v-model="form.image"
            ><template #append
              ><el-button :loading="inspectingImage" @click="inspectImage"
                >分析镜像</el-button
              ></template
            ></el-input
          ></el-form-item
        ><el-alert
          v-if="imageMetadata"
          class="image-metadata"
          type="info"
          :closable="false"
          show-icon
        >
          <template #title
            >镜像声明已载入，未显式覆盖的 Entrypoint / Cmd 会由 OCI 镜像继承。</template
          >
          <div class="image-metadata__grid">
            <span>端口：{{ imageMetadata.exposedPorts.join(', ') || '无' }}</span>
            <span>卷：{{ imageMetadata.volumes.join(', ') || '无' }}</span>
            <span>Entrypoint：{{ imageMetadata.entrypoint.join(' ') || '默认' }}</span>
            <span>Cmd：{{ imageMetadata.command.join(' ') || '默认' }}</span>
          </div> </el-alert
        ><el-form-item label="端口映射"
          ><el-input v-model="form.ports" placeholder="0.0.0.0:8088:80/tcp" /><small
            class="form-tip"
            >多个映射用逗号分隔；0.0.0.0 对所有网卡开放，127.0.0.1 仅供本机访问。</small
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
        ><template v-if="form.autoupdate"
          ><el-form-item label="允许版本范围"
            ><el-input v-model="form.versionRange" placeholder="*、1.4.*、>=1.4.0 或 1.4.2" /><small
              class="form-tip"
              >镜像无可识别版本时只能使用 *；更新前会再次校验。</small
            ></el-form-item
          ><el-form-item label="维护窗口"
            ><el-input v-model="form.maintenanceWindow" placeholder="Sun@20:00-23:59" /><small
              class="form-tip"
              >使用 Asia/Shanghai 时区，自动更新仅在该窗口执行。</small
            ></el-form-item
          ><el-form-item label="依赖应用（可选）"
            ><el-input
              v-model="form.dependencies"
              placeholder="postgres-exporter,metrics-gateway"
            /><small class="form-tip">逗号分隔；依赖未安装时自动更新会被阻止。</small></el-form-item
          ></template
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

    <el-drawer
      v-model="detailVisible"
      title="应用详情"
      size="min(760px, 96vw)"
      destroy-on-close
      @closed="closeDetail"
    >
      <div v-loading="detailLoading" class="application-detail">
        <template v-if="detail">
          <el-descriptions :column="2" border>
            <el-descriptions-item label="应用">{{
              detail.installation.displayName
            }}</el-descriptions-item>
            <el-descriptions-item label="状态">{{
              detail.installation.state
            }}</el-descriptions-item>
            <el-descriptions-item label="当前版本">{{
              detail.installation.installedVersion
            }}</el-descriptions-item>
            <el-descriptions-item label="自动更新">{{
              detail.installation.autoUpdateEnabled ? '启用' : '关闭'
            }}</el-descriptions-item>
            <el-descriptions-item label="模板"
              >{{ detail.installation.templateId }} /
              {{ detail.installation.templateVersion }}</el-descriptions-item
            >
            <el-descriptions-item label="安装时间">{{
              new Date(detail.installation.installedAtUtc).toLocaleString()
            }}</el-descriptions-item>
          </el-descriptions>

          <section>
            <h3>关联资源</h3>
            <el-table :data="detail.resources" empty-text="暂无关联资源">
              <el-table-column prop="role" label="角色" width="130" />
              <el-table-column prop="name" label="资源" min-width="160" />
              <el-table-column prop="type" label="类型" width="140" />
              <el-table-column prop="state" label="状态" width="110" />
              <el-table-column
                prop="version"
                label="版本 / 镜像"
                min-width="200"
                show-overflow-tooltip
              />
            </el-table>
          </section>

          <section>
            <h3>更新历史</h3>
            <el-table :data="detail.updateHistory" empty-text="暂无更新记录">
              <el-table-column label="时间" min-width="170">
                <template #default="{ row }">{{
                  new Date(row.startedAtUtc).toLocaleString()
                }}</template>
              </el-table-column>
              <el-table-column prop="result" label="结果" width="100" />
              <el-table-column prop="versionPolicy" label="版本策略" width="110" />
              <el-table-column
                prop="newImageDigest"
                label="镜像摘要"
                min-width="230"
                show-overflow-tooltip
              />
              <el-table-column label="回滚" width="80">
                <template #default="{ row }">{{ row.wasRolledBack ? '是' : '否' }}</template>
              </el-table-column>
            </el-table>
          </section>
        </template>
      </div>
    </el-drawer>
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
.image-metadata {
  margin-bottom: 14px;
}
.image-metadata__grid {
  display: grid;
  gap: 4px;
  margin-top: 8px;
  overflow-wrap: anywhere;
  color: var(--el-text-color-regular);
  font-size: 12px;
}
.application-detail {
  display: grid;
  gap: 22px;
  min-height: 180px;
}
.application-detail h3 {
  margin: 0 0 10px;
  font-size: 14px;
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
