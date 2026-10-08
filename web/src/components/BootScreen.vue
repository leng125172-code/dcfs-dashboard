<script setup lang="ts">
const whaleMarkUrl = '/images/whaledeck-mark.png'
</script>

<template>
  <div
    class="boot-screen"
    role="status"
    aria-live="polite"
    aria-label="正在启动 WhaleDeck 协同平台"
  >
    <div class="boot-screen__glow" aria-hidden="true" />
    <div class="boot-screen__content">
      <span class="boot-screen__mark">
        <img :src="whaleMarkUrl" alt="" />
      </span>
      <strong>WhaleDeck 协同平台</strong>
      <small>WORKSTATION CONTROL PLANE</small>
      <span class="boot-screen__dots" aria-hidden="true"><i /><i /><i /></span>
      <p>正在连接身份与平台服务</p>
    </div>
  </div>
</template>
