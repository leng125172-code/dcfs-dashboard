<script setup lang="ts">
import { Back, HomeFilled } from '@element-plus/icons-vue'
import AppTopbar from '@/components/AppTopbar.vue'

defineProps<{ code: string; title: string; description: string }>()
</script>

<template>
  <div class="state-page">
    <AppTopbar login-mode />
    <main class="state-page__card panel">
      <span>{{ code }}</span>
      <h1>{{ title }}</h1>
      <p>{{ description }}</p>
      <div>
        <el-button @click="$router.back()"
          ><el-icon><Back /></el-icon>返回</el-button
        >
        <el-button type="primary" @click="$router.push('/')"
          ><el-icon><HomeFilled /></el-icon>首页</el-button
        >
      </div>
    </main>
  </div>
</template>

<style scoped>
.state-page {
  min-height: 100vh;
  padding-top: calc(var(--whaledeck-topbar-height) + 8vh);
  background: var(--whaledeck-page-bg);
}
.state-page__card {
  width: min(560px, calc(100% - 36px));
  margin: auto;
  padding: 44px;
  text-align: center;
}
.state-page__card > span {
  color: var(--el-color-primary);
  font-size: 54px;
  font-weight: 800;
}
.state-page__card h1 {
  margin: 8px 0;
  font-size: 24px;
}
.state-page__card p {
  margin: 0 0 28px;
  color: var(--el-text-color-secondary);
}
</style>
