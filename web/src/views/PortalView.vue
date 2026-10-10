<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { Delete, Edit, Link, Plus, RefreshRight } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { apiRequest } from '@/services/apiClient'
import { authSession } from '@/services/authSession'
import type { PortalItem } from '@/services/contracts'

const items = ref<PortalItem[]>([])
const loading = ref(false)
const dialog = ref(false)
const saving = ref(false)
const form = reactive({
  id: '',
  scope: 'Personal' as 'Personal' | 'Public',
  name: '',
  description: '',
  url: 'http://',
  iconValue: 'Link',
  color: 'primary',
  version: 0,
})
const canPublish = computed(() => authSession.user.value?.isAdministrator === true)

async function load() {
  loading.value = true
  try {
    items.value = await apiRequest<PortalItem[]>('portals')
  } finally {
    loading.value = false
  }
}

function openCreate() {
  Object.assign(form, {
    id: '',
    scope: 'Personal',
    name: '',
    description: '',
    url: 'http://',
    iconValue: 'Link',
    color: 'primary',
    version: 0,
  })
  dialog.value = true
}

function openEdit(item: PortalItem) {
  Object.assign(form, {
    id: item.id,
    scope: item.scope,
    name: item.name,
    description: item.description || '',
    url: item.url,
    iconValue: item.iconValue,
    color: item.color,
    version: item.version,
  })
  dialog.value = true
}

async function save() {
  if (!form.name.trim() || !/^https?:\/\//i.test(form.url)) {
    ElMessage.warning('请填写名称和有效的 HTTP/HTTPS 地址')
    return
  }
  saving.value = true
  const body = JSON.stringify({
    id: form.id || null,
    scope: form.scope,
    name: form.name,
    description: form.description || null,
    url: form.url,
    iconKind: 'BuiltIn',
    iconValue: form.iconValue,
    color: form.color,
    sortOrder: form.id
      ? (items.value.find((item) => item.id === form.id)?.sortOrder ?? items.value.length)
      : items.value.length,
    isEnabled: true,
    expectedVersion: form.id ? form.version : null,
  })
  try {
    await apiRequest<PortalItem>(form.id ? `portals/${form.id}` : 'portals', {
      method: form.id ? 'PUT' : 'POST',
      body,
    })
    dialog.value = false
    ElMessage.success('门户入口已保存')
    await load()
  } finally {
    saving.value = false
  }
}

async function remove(item: PortalItem) {
  await ElMessageBox.confirm(`删除门户入口“${item.name}”？`, '删除确认', {
    type: 'warning',
    confirmButtonText: '删除',
    cancelButtonText: '取消',
  })
  await apiRequest<void>(`portals/${item.id}`, { method: 'DELETE' })
  ElMessage.success('门户入口已删除')
  await load()
}

onMounted(load)
</script>

<template>
  <div class="portal-page">
    <header class="page-heading">
      <div>
        <span class="panel__eyebrow">MY PORTAL</span>
        <h1>我的门户</h1>
        <p>管理自己的常用入口；管理员还可以发布公共入口。</p>
      </div>
      <div class="page-heading__actions">
        <el-button :loading="loading" @click="load"
          ><el-icon><RefreshRight /></el-icon>刷新</el-button
        ><el-button type="primary" @click="openCreate"
          ><el-icon><Plus /></el-icon>新增入口</el-button
        >
      </div>
    </header>
    <section class="portal-grid" v-loading="loading">
      <el-empty v-if="!loading && items.length === 0" description="还没有门户入口" />
      <article v-for="item in items" :key="item.id" class="panel portal-card">
        <a :href="item.url" target="_blank" rel="noopener noreferrer"
          ><span class="portal-card__icon"
            ><el-icon><Link /></el-icon></span
          ><span
            ><strong>{{ item.name }}</strong
            ><small>{{ item.description || item.url }}</small></span
          ></a
        >
        <div>
          <el-tag size="small" effect="plain">{{
            item.scope === 'Public' ? '公共' : '个人'
          }}</el-tag
          ><el-button text circle aria-label="编辑" @click="openEdit(item)"
            ><el-icon><Edit /></el-icon></el-button
          ><el-button text circle type="danger" aria-label="删除" @click="remove(item)"
            ><el-icon><Delete /></el-icon
          ></el-button>
        </div>
      </article>
    </section>

    <el-dialog
      v-model="dialog"
      :title="form.id ? '编辑门户入口' : '新增门户入口'"
      width="min(520px, calc(100vw - 32px))"
    >
      <el-form label-position="top">
        <el-form-item label="名称"
          ><el-input v-model="form.name" maxlength="80" show-word-limit
        /></el-form-item>
        <el-form-item label="地址"
          ><el-input v-model="form.url" placeholder="http://192.168.22.19:8080"
        /></el-form-item>
        <el-form-item label="说明"
          ><el-input v-model="form.description" maxlength="240"
        /></el-form-item>
        <el-form-item v-if="canPublish" label="范围"
          ><el-segmented
            v-model="form.scope"
            :options="[
              { label: '个人', value: 'Personal' },
              { label: '公共', value: 'Public' },
            ]"
        /></el-form-item>
      </el-form>
      <template #footer
        ><el-button @click="dialog = false">取消</el-button
        ><el-button type="primary" :loading="saving" @click="save">保存</el-button></template
      >
    </el-dialog>
  </div>
</template>

<style scoped>
.portal-page {
  display: grid;
  gap: 18px;
}
.portal-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 14px;
  min-height: 180px;
}
.portal-grid > .el-empty {
  grid-column: 1 / -1;
}
.portal-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 16px;
}
.portal-card a {
  display: flex;
  min-width: 0;
  align-items: center;
  gap: 12px;
  color: inherit;
  text-decoration: none;
}
.portal-card a > span:last-child {
  display: grid;
  min-width: 0;
}
.portal-card small {
  overflow: hidden;
  color: var(--el-text-color-secondary);
  text-overflow: ellipsis;
  white-space: nowrap;
}
.portal-card__icon {
  display: grid;
  width: 42px;
  height: 42px;
  place-items: center;
  border-radius: 10px;
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}
.portal-card > div {
  display: flex;
  align-items: center;
}
@media (max-width: 900px) {
  .portal-grid {
    grid-template-columns: 1fr;
  }
}
</style>
