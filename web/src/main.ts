import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { ElButton, ElIcon, ElMenu, ElMenuItem, ElTag } from 'element-plus'
import 'element-plus/dist/index.css'
import 'element-plus/theme-chalk/dark/css-vars.css'

import App from './App.vue'
import router from './router'
import './styles/main.css'

const app = createApp(App)

app.use(createPinia())
app.use(router)
app.component(ElButton.name!, ElButton)
app.component(ElIcon.name!, ElIcon)
app.component(ElMenu.name!, ElMenu)
app.component(ElMenuItem.name!, ElMenuItem)
app.component(ElTag.name!, ElTag)

app.mount('#app')
