import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';
import { icuFoliaDataPlugin } from './vendor/folia-major/dev/icuFolia/icuFoliaDataPlugin.mjs';
import { traditionalLocalePlugin } from './vendor/folia-major/dev/i18n/traditionalLocalePlugin.mjs';
import { commandPinyinPlugin } from './vendor/folia-major/dev/pinyin/commandPinyinPlugin.mjs';

export default defineConfig({
  base: './',
  plugins: [react(), icuFoliaDataPlugin(), traditionalLocalePlugin(), commandPinyinPlugin()],
  publicDir: false,
  worker: { format: 'es' },
  resolve: { alias: { '@neteasecloudmusicapienhanced/api/package.json': fileURLToPath(new URL('./netease-backend-package.json', import.meta.url)) } },
  define: { __BUILD_COMMIT__: JSON.stringify('7a5f975e337e5ba13f55d72b5bc68983bf41c379') },
  build: { outDir: 'dist', emptyOutDir: true, assetsInlineLimit: 0, chunkSizeWarningLimit: 5000 },
});
