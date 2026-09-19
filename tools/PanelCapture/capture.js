// Fase 12 - captura best-effort de screenshots (dashboard/Grafana/Prometheus) para
// C:\git\norn-results, usada por run-experiment.ps1 (--mode execution) e run-campaign.ps1
// (--mode block). Nunca falha a campanha por causa disto: toda captura individual é
// try/catch e o processo sempre sai com código 0 - perder um screenshot não pode custar
// uma execução inteira de ~25 min.
//
// Uso:
//   node capture.js --out <dir> --mode block
//   node capture.js --out <dir> --mode execution --prom-expr "<promql>" --prom-range 30m --label F3-C-rep1

const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i++) {
    if (argv[i].startsWith('--')) {
      const key = argv[i].slice(2);
      const value = argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[++i] : true;
      args[key] = value;
    }
  }
  return args;
}

const args = parseArgs(process.argv.slice(2));
const outDir = args.out;
const mode = args.mode;
const label = args.label ? `${args.label}-` : '';
const dashboardUrl = args['dashboard-url'] || 'http://localhost:5080';
const grafanaUrl = args['grafana-url'] || 'http://localhost:3000';
const prometheusUrl = args['prometheus-url'] || 'http://localhost:9090';
const promExpr = args['prom-expr'];
const promRange = args['prom-range'] || '30m';

if (!outDir || !mode) {
  console.error('Uso: node capture.js --out <dir> --mode block|execution [--prom-expr "<promql>" --prom-range 30m] [--label <texto>]');
  process.exit(1);
}

fs.mkdirSync(outDir, { recursive: true });

async function shoot(page, name, url, opts = {}) {
  try {
    await page.goto(url, { waitUntil: opts.waitUntil || 'networkidle', timeout: 20000 });
    await page.waitForTimeout(opts.settleMs || 1200);
    if (opts.clickTab) {
      await page.getByRole('tab', { name: opts.clickTab }).click({ timeout: 5000 });
      await page.waitForTimeout(1500);
    }
    const file = path.join(outDir, `${label}${name}.png`);
    await page.screenshot({ path: file });
    console.log(`OK   ${name} -> ${file}`);
  } catch (err) {
    console.log(`FALHOU ${name}: ${err.message.split('\n')[0]}`);
  }
}

// Achado do code-reviewer antes do commit: a IIFE em si (chromium.launch/newPage/close) não
// tinha proteção nenhuma, só as chamadas individuais dentro de shoot() — uma falha aqui (ex.:
// Chromium não instalado nesta máquina) é promise rejeitada sem handler, que derruba o processo
// com código != 0 em Node 15+, contradizendo o próprio contrato do cabeçalho ("sempre sai com
// código 0"). Try/catch/finally em volta do corpo inteiro, sempre saindo 0.
(async () => {
  let browser;
  try {
    browser = await chromium.launch();
    const page = await browser.newPage({ viewport: { width: 1600, height: 1000 } });

    // Comum aos dois modos: visão geral do dashboard proprio do Norn no instante da captura.
    await shoot(page, 'norn-dashboard', `${dashboardUrl}/`);

    if (mode === 'block') {
      await shoot(page, 'grafana-norn-platform', `${grafanaUrl}/d/norn-platform/norn-platform?orgId=1`);
      await shoot(page, 'grafana-shop-overview', `${grafanaUrl}/d/norn-shop-overview/shop-overview?orgId=1`);
    } else if (mode === 'execution' && promExpr) {
      const url = `${prometheusUrl}/graph?g0.expr=${encodeURIComponent(promExpr)}&g0.range_input=${encodeURIComponent(promRange)}`;
      await shoot(page, 'prometheus-signature', url, { waitUntil: 'load', clickTab: 'Graph' });
    } else if (mode === 'execution') {
      console.log('SKIP prometheus-signature: --prom-expr ausente');
    }
  } catch (err) {
    console.log(`FALHOU (fatal, fora de shoot()): ${err.message.split('\n')[0]}`);
  } finally {
    if (browser) { await browser.close().catch(() => {}); }
    process.exit(0);
  }
})();
