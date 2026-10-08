<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterView, useRoute, useRouter } from 'vue-router'
import BootScreen from '@/components/BootScreen.vue'
import { authSession } from '@/services/authSession'

const minimumBootDuration = 3000
const route = useRoute()
const router = useRouter()
const isBooting = ref(true)

onMounted(async () => {
  await Promise.all([
    authSession.initialize(),
    new Promise((resolve) => window.setTimeout(resolve, minimumBootDuration)),
  ])

  if (!authSession.authenticated.value && route.meta.requiresAuth) {
    await router.replace({ name: 'login', query: { returnUrl: route.fullPath } })
  } else if (authSession.authenticated.value && route.name === 'login') {
    await router.replace({ name: 'overview' })
  }

  isBooting.value = false
})
</script>

<template>
  <RouterView v-slot="{ Component }">
    <Transition name="boot-fade" mode="out-in">
      <BootScreen v-if="isBooting" key="boot" />
      <component :is="Component" v-else />
    </Transition>
  </RouterView>
</template>
