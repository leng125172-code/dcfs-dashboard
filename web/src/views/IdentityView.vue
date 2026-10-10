<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { apiRequest } from '@/services/apiClient'
import { enqueueOperation, stageSecret } from '@/services/operations'
import type { ManagedResource, RoleMapping } from '@/services/contracts'

const route = useRoute()
const mode = computed(() => String(route.meta.identityMode || 'users'))
const title = computed(() => String(route.meta.title || '身份目录'))
const items = ref<ManagedResource[]>([])
const roleMappings = ref<RoleMapping[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const editing = ref<ManagedResource | null>(null)
const form = reactive({
  username: '',
  name: '',
  email: '',
  password: '',
  slug: '',
  redirectUri: '',
  openInNewTab: false,
})

async function load() {
  loading.value = true
  try {
    if (mode.value === 'groups') {
      const [groups, mappings] = await Promise.all([
        apiRequest<ManagedResource[]>('groups'),
        apiRequest<RoleMapping[]>('identity/role-mappings'),
      ])
      items.value = groups
      roleMappings.value = mappings
    } else {
      items.value = await apiRequest<ManagedResource[]>(mode.value)
      roleMappings.value = []
    }
  } finally {
    loading.value = false
  }
}
function roleMapping(item: ManagedResource) {
  return roleMappings.value.find((mapping) => mapping.authentikGroupId === item.id)
}
async function toggleAdministrator(item: ManagedResource) {
  const mapping = roleMapping(item)
  if (mapping?.isEnabled) {
    if (!mapping.isMutable) return
    await ElMessageBox.confirm(
      `取消用户组“${item.name}”的 Whale Deck 管理员权限？权限快照会立即失效。`,
      '取消管理员映射',
      { type: 'warning' },
    )
    await apiRequest(
      `identity/role-mappings/${encodeURIComponent(mapping.id || '')}?version=${mapping.version}`,
      { method: 'DELETE' },
    )
    ElMessage.success('管理员组映射已取消')
  } else {
    await ElMessageBox.confirm(
      `将 Authentik 用户组“${item.name}”映射为 Whale Deck 管理员？`,
      '新增管理员映射',
      { type: 'warning' },
    )
    await apiRequest<RoleMapping>('identity/role-mappings', {
      method: 'POST',
      body: JSON.stringify({
        authentikGroupId: item.id,
        authentikGroupName: item.name,
        isEnabled: true,
      }),
    })
    ElMessage.success('管理员组映射已生效')
  }
  await load()
}
function open(item?: ManagedResource) {
  editing.value = item || null
  Object.assign(form, {
    username: '',
    name: item?.name || '',
    email: '',
    password: '',
    slug: '',
    redirectUri: '',
    openInNewTab: false,
  })
  dialogVisible.value = true
}
async function submit() {
  let action = ''
  const parameters: Record<string, string> = {}
  if (mode.value === 'users') {
    action = editing.value ? 'update-user' : 'create-user'
    if (!editing.value) parameters.username = form.username.trim()
    parameters.name = form.name.trim()
    parameters.email = form.email.trim()
    if (!editing.value && form.password) {
      const ticket = await stageSecret(form.password)
      parameters.inputTicket = ticket.token
      parameters.inputKind = 'password'
    }
  } else {
    action = editing.value ? 'update-sso' : 'create-sso'
    parameters.name = form.name.trim()
    parameters.openInNewTab = String(form.openInNewTab)
    if (!editing.value)
      Object.assign(parameters, {
        slug: form.slug.trim(),
        providerType: 'oauth2',
        redirectUri: form.redirectUri.trim(),
      })
  }
  const job = await enqueueOperation(
    'identity',
    action,
    editing.value?.id || null,
    parameters,
    false,
  )
  dialogVisible.value = false
  ElMessage.success(`任务已提交：${job.id}`)
}
async function disable(item: ManagedResource) {
  await ElMessageBox.confirm(
    `禁用 Authentik 用户“${item.name}”？其现有管理权限最长在策略缓存到期后失效。`,
    '禁用用户',
    { type: 'warning' },
  )
  const job = await enqueueOperation('identity', 'disable-user', item.id, {}, false)
  ElMessage.success(`任务已提交：${job.id}`)
}

onMounted(load)
watch(() => route.fullPath, load)
</script>

<template>
  <div class="identity-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">AUTHENTIK</span>
        <h1>{{ title }}</h1>
        <p>身份、组和 SSO Provider 均以 Authentik 为唯一事实来源。</p>
      </div>
      <div class="page-heading__actions">
        <el-button :loading="loading" @click="load">刷新</el-button
        ><el-button v-if="mode !== 'groups'" type="primary" @click="open()">{{
          mode === 'users' ? '创建用户' : '创建 SSO 应用'
        }}</el-button>
      </div>
    </header>
    <section class="panel" v-loading="loading">
      <el-table :data="items" row-key="id" empty-text="暂无身份数据"
        ><el-table-column prop="name" label="名称" min-width="220" /><el-table-column
          prop="id"
          label="Authentik ID"
          min-width="260"
          show-overflow-tooltip
        /><el-table-column label="状态" width="120"
          ><template #default="scope"
            ><el-tag :type="scope.row.state === 'Active' ? 'success' : 'info'">{{
              scope.row.state
            }}</el-tag></template
          ></el-table-column
        ><el-table-column v-if="mode === 'groups'" label="平台角色" min-width="180"
          ><template #default="scope"
            ><el-tag v-if="roleMapping(scope.row)?.isEnabled" type="primary"
              >管理员 ·
              {{
                roleMapping(scope.row)?.source === 'Deployment' ? '部署配置' : '平台映射'
              }}</el-tag
            ><span v-else class="muted">普通用户组</span></template
          ></el-table-column
        ><el-table-column label="操作" width="190"
          ><template #default="scope"
            ><template v-if="mode === 'groups'"
              ><el-button
                link
                :type="roleMapping(scope.row)?.isEnabled ? 'danger' : 'primary'"
                :disabled="roleMapping(scope.row)?.isEnabled && !roleMapping(scope.row)?.isMutable"
                @click="toggleAdministrator(scope.row)"
                >{{ roleMapping(scope.row)?.isEnabled ? '取消管理员' : '设为管理员' }}</el-button
              ></template
            ><template v-else
              ><el-button link type="primary" @click="open(scope.row)">编辑</el-button
              ><el-button
                v-if="mode === 'users'"
                link
                type="danger"
                :disabled="scope.row.state !== 'Active'"
                @click="disable(scope.row)"
                >禁用</el-button
              ></template
            ></template
          ></el-table-column
        ></el-table
      >
    </section>
    <el-dialog
      v-model="dialogVisible"
      :title="
        editing ? '编辑身份对象' : mode === 'users' ? '创建 Authentik 用户' : '创建 OIDC 应用'
      "
      width="min(620px, calc(100vw - 32px))"
      ><el-form label-position="top">
        <template v-if="mode === 'users'"
          ><el-form-item v-if="!editing" label="用户名"
            ><el-input v-model="form.username" autocomplete="off" /></el-form-item
          ><el-form-item label="显示名称"><el-input v-model="form.name" /></el-form-item
          ><el-form-item label="电子邮箱"
            ><el-input v-model="form.email" type="email" /></el-form-item
          ><el-form-item v-if="!editing" label="初始密码（可选）"
            ><el-input
              v-model="form.password"
              type="password"
              show-password
              autocomplete="new-password"
            /><small
              >密码通过一次性 Secret 交给 Authentik，不进入浏览器持久存储。</small
            ></el-form-item
          ></template
        >
        <template v-else
          ><el-form-item label="应用名称"><el-input v-model="form.name" /></el-form-item
          ><el-form-item v-if="!editing" label="Slug"><el-input v-model="form.slug" /></el-form-item
          ><el-form-item v-if="!editing" label="回调地址"
            ><el-input v-model="form.redirectUri" /></el-form-item
          ><small v-if="!editing"
            >授权与失效流程由 Whale Deck 从 Authentik 的批准默认流程自动解析。</small
          >
          ><el-form-item label="在新窗口打开"
            ><el-switch v-model="form.openInNewTab" /></el-form-item
        ></template> </el-form
      ><template #footer
        ><el-button @click="dialogVisible = false">取消</el-button
        ><el-button type="primary" @click="submit">提交</el-button></template
      ></el-dialog
    >
  </div>
</template>

<style scoped>
.identity-page {
  display: grid;
  gap: 18px;
}
.identity-page small {
  display: block;
  color: var(--el-text-color-secondary);
  font-size: 11px;
  margin-top: 5px;
}
.muted {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
</style>
