<script setup lang="ts">
import { computed, useId } from 'vue'
import type { TelemetrySeries } from '@/services/workstationOverview'

const props = defineProps<{
  labels: string[]
  series: TelemetrySeries[]
  unit: string
}>()

const chartId = useId().replace(/:/g, '')
const plot = { left: 58, top: 18, width: 912, height: 190 }

const maximum = computed(() => {
  const highest = Math.max(1, ...props.series.flatMap((item) => item.values))
  return Math.ceil(highest * 1.15)
})

const gridLines = computed(() =>
  Array.from({ length: 5 }, (_, index) => {
    const ratio = index / 4
    return {
      y: plot.top + ratio * plot.height,
      value: maximum.value * (1 - ratio),
    }
  }),
)

function point(value: number, index: number, count: number) {
  const x = plot.left + (count <= 1 ? 0 : (index / (count - 1)) * plot.width)
  const y = plot.top + plot.height - (value / maximum.value) * plot.height
  return { x, y }
}

function linePath(values: number[]) {
  return values
    .map((value, index) => {
      const current = point(value, index, values.length)
      return `${index === 0 ? 'M' : 'L'} ${current.x.toFixed(2)} ${current.y.toFixed(2)}`
    })
    .join(' ')
}

function areaPath(values: number[]) {
  if (values.length === 0) return ''
  const first = point(values[0] ?? 0, 0, values.length)
  const last = point(values[values.length - 1] ?? 0, values.length - 1, values.length)
  return `${linePath(values)} L ${last.x.toFixed(2)} ${(plot.top + plot.height).toFixed(2)} L ${first.x.toFixed(2)} ${(plot.top + plot.height).toFixed(2)} Z`
}

function formatTick(value: number) {
  if (value >= 100) return Math.round(value).toString()
  if (value >= 10) return value.toFixed(0)
  return value.toFixed(1)
}
</script>

<template>
  <div class="telemetry-chart" role="img" :aria-label="`监控趋势图，单位 ${unit}`">
    <svg viewBox="0 0 1000 250" preserveAspectRatio="none" aria-hidden="true">
      <defs>
        <linearGradient
          v-for="(item, index) in series"
          :id="`${chartId}-gradient-${index}`"
          :key="`${item.name}-gradient`"
          x1="0"
          x2="0"
          y1="0"
          y2="1"
        >
          <stop offset="0%" :stop-color="item.color" stop-opacity="0.24" />
          <stop offset="100%" :stop-color="item.color" stop-opacity="0.02" />
        </linearGradient>
      </defs>

      <g v-for="grid in gridLines" :key="grid.y">
        <line
          class="telemetry-chart__grid"
          :x1="plot.left"
          :x2="plot.left + plot.width"
          :y1="grid.y"
          :y2="grid.y"
        />
        <text class="telemetry-chart__tick" x="49" :y="grid.y + 4" text-anchor="end">
          {{ formatTick(grid.value) }}
        </text>
      </g>

      <path
        v-for="(item, index) in series"
        :key="`${item.name}-area`"
        :d="areaPath(item.values)"
        :fill="`url(#${chartId}-gradient-${index})`"
      />
      <path
        v-for="item in series"
        :key="`${item.name}-line`"
        class="telemetry-chart__line"
        :d="linePath(item.values)"
        fill="none"
        :stroke="item.color"
      />

      <g v-for="(label, index) in labels" :key="`${label}-${index}`">
        <text
          class="telemetry-chart__label"
          :x="point(0, index, labels.length).x"
          y="236"
          text-anchor="middle"
        >
          {{ label }}
        </text>
      </g>
    </svg>
  </div>
</template>

<style scoped>
.telemetry-chart {
  overflow-x: auto;
  width: 100%;
}

.telemetry-chart svg {
  display: block;
  min-width: 680px;
  width: 100%;
  height: 250px;
}

.telemetry-chart__grid {
  stroke: var(--el-border-color-lighter);
  stroke-dasharray: 4 5;
  vector-effect: non-scaling-stroke;
}

.telemetry-chart__line {
  stroke-linecap: round;
  stroke-linejoin: round;
  stroke-width: 2.2;
  vector-effect: non-scaling-stroke;
}

.telemetry-chart__tick,
.telemetry-chart__label {
  fill: var(--el-text-color-secondary);
  font-family: var(--whaledeck-font-family);
  font-size: 11px;
}

@media (max-width: 680px) {
  .telemetry-chart svg {
    height: 220px;
  }
}
</style>
