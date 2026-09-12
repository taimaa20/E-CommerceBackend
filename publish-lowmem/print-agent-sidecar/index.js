const fs = require('fs');
const net = require('net');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

// Pin Puppeteer's browser cache to a folder next to this script.
// Without this, the LocalSystem service looks in
// C:\WINDOWS\system32\config\systemprofile\.cache\puppeteer and finds nothing.
// Must be set BEFORE require('puppeteer').
const PUPPETEER_CACHE_DIR =
  process.env.PUPPETEER_CACHE_DIR || path.join(__dirname, '.puppeteer-cache');
process.env.PUPPETEER_CACHE_DIR = PUPPETEER_CACHE_DIR;

const puppeteer = require('puppeteer');

const PDF_MODE = 'pdf';
const ESC_POS_RASTER_MODE = 'escpos-raster';
const RENDER_ESC_POS_RASTER_MODE = 'render-escpos-raster';
const PAGE_VIEWPORT_WIDTH_PX = 302;
const INITIAL_VIEWPORT_HEIGHT_PX = 800;
const PRINTER_WIDTH_PX = 576;
const LUMINANCE_THRESHOLD = 128;
const RASTER_CHUNK_ROWS = 200;
const LINE_BREAKS_BEFORE_CUT = 6;
const TCP_CHUNK_SIZE = 1024;
const TCP_CHUNK_DELAY_MS = 12;
const TCP_POST_WRITE_HOLD_MS = 1000;

// Resolve chrome.exe explicitly. Puppeteer's auto-discovery sometimes fails
// when the process runs as a Windows service even though the path and version
// are correct, so we walk the cache and pick the newest chrome.exe ourselves.
// Resolve when every <img> on the page is either loaded or finished failing,
// or after maxMs whichever comes first. Lets receipts print offline even when
// external image URLs (logo, QR code) are unreachable.
async function waitForImagesOrTimeout(page, maxMs) {
  await page.evaluate((max) => new Promise((resolve) => {
    const imgs = Array.from(document.images);
    if (imgs.length === 0) { resolve(); return; }
    let pending = imgs.filter((img) => !img.complete).length;
    if (pending === 0) { resolve(); return; }
    const done = () => { if (--pending <= 0) resolve(); };
    imgs.forEach((img) => {
      if (img.complete) return;
      img.addEventListener('load', done, { once: true });
      img.addEventListener('error', done, { once: true });
    });
    setTimeout(resolve, max);
  }), maxMs);
}

function findChromeExecutable() {
  if (process.env.PUPPETEER_EXECUTABLE_PATH && fs.existsSync(process.env.PUPPETEER_EXECUTABLE_PATH)) {
    return process.env.PUPPETEER_EXECUTABLE_PATH;
  }
  const chromeRoot = path.join(PUPPETEER_CACHE_DIR, 'chrome');
  if (!fs.existsSync(chromeRoot)) return null;
  const versions = fs.readdirSync(chromeRoot)
    .map((dir) => path.join(chromeRoot, dir, 'chrome-win64', 'chrome.exe'))
    .filter((p) => fs.existsSync(p));
  return versions.length ? versions[versions.length - 1] : null;
}

const NETWORK_TIMEOUT_MS = 8000;

async function main() {
  const input = JSON.parse(await readStdin());
  const copies = Math.max(1, Number(input.copies || 1));

  if ((input.mode || PDF_MODE) === RENDER_ESC_POS_RASTER_MODE) {
    const escPosBuffer = await renderEscPosRaster(input.html);
    writeResult({ success: true, payloadBase64: escPosBuffer.toString('base64') });
    return;
  }

  if ((input.mode || PDF_MODE) === ESC_POS_RASTER_MODE && input.type === 'network') {
    const escPosBuffer = await renderEscPosRaster(input.html);
    await printNetwork(escPosBuffer, input.printerIp, Number(input.printerPort || 9100), copies);
    writeResult({ success: true });
    return;
  }

  const pdfBuffer = await renderPdf(input.html);

  if (input.type === 'usb') {
    printUsb(pdfBuffer, input.printerName, copies);
    writeResult({ success: true });
    return;
  }

  if (input.type === 'network') {
    await printNetwork(pdfBuffer, input.printerIp, Number(input.printerPort || 9100), copies);
    writeResult({ success: true });
    return;
  }

  throw new Error(`Unsupported printer type: ${input.type}`);
}

function readStdin() {
  return new Promise((resolve, reject) => {
    const chunks = [];
    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (chunk) => chunks.push(chunk));
    process.stdin.on('end', () => resolve(chunks.join('')));
    process.stdin.on('error', reject);
  });
}

async function renderPdf(html) {
  if (!html) throw new Error('html is required');

  const executablePath = findChromeExecutable();
  if (!executablePath) {
    throw new Error(`Chrome not found under ${PUPPETEER_CACHE_DIR}. Run: npx puppeteer browsers install chrome`);
  }

  const browser = await puppeteer.launch({
    headless: true,
    executablePath,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage'],
  });

  try {
    const page = await browser.newPage();
    const clip = await preparePage(page, html);

    return await page.pdf({
      width: '80mm',
      height: `${Math.ceil(clip.height)}px`,
      printBackground: true,
      margin: { top: 0, right: 0, bottom: 0, left: 0 },
      scale: 1,
    });
  } finally {
    await browser.close();
  }
}

async function renderEscPosRaster(html) {
  if (!html) throw new Error('html is required');

  const executablePath = findChromeExecutable();
  if (!executablePath) {
    throw new Error(`Chrome not found under ${PUPPETEER_CACHE_DIR}. Run: npx puppeteer browsers install chrome`);
  }

  const browser = await puppeteer.launch({
    headless: true,
    executablePath,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage'],
  });

  try {
    const page = await browser.newPage();
    const clip = await preparePage(page, html);
    const pngBuffer = await page.screenshot({
      type: 'png',
      clip,
      captureBeyondViewport: true,
      omitBackground: false,
    });

    return await pngToEscPos(page, pngBuffer);
  } finally {
    await browser.close();
  }
}

async function preparePage(page, html) {
  await page.setViewport({ width: PAGE_VIEWPORT_WIDTH_PX, height: INITIAL_VIEWPORT_HEIGHT_PX, deviceScaleFactor: 2 });
  await page.setContent(html, {
    waitUntil: 'domcontentloaded',
    timeout: 15000,
  });
  await waitForImagesOrTimeout(page, 4000);

  const clip = await getContentClip(page);
  await page.setViewport({
    width: PAGE_VIEWPORT_WIDTH_PX,
    height: Math.max(Math.ceil(clip.height), 1),
    deviceScaleFactor: 2,
  });
  return clip;
}

async function getContentClip(page) {
  const clip = await page.evaluate((viewportWidth) => {
    const preferredRoot =
      document.querySelector('.ticket') ||
      document.querySelector('[data-print-root]') ||
      document.querySelector('.receipt');

    const rects = preferredRoot
      ? [preferredRoot.getBoundingClientRect()]
      : Array.from(document.body.children)
        .map((el) => el.getBoundingClientRect())
        .filter((rect) => rect.width > 0 && rect.height > 0);

    const usable = rects.filter((rect) => rect.width > 0 && rect.height > 0);
    if (usable.length === 0) {
      return { x: 0, y: 0, width: viewportWidth, height: 1 };
    }

    const left = Math.max(0, Math.floor(Math.min(...usable.map((rect) => rect.left))));
    const top = Math.max(0, Math.floor(Math.min(...usable.map((rect) => rect.top))));
    const right = Math.ceil(Math.max(...usable.map((rect) => rect.right)));
    const bottom = Math.ceil(Math.max(...usable.map((rect) => rect.bottom)));

    return {
      x: left,
      y: top,
      width: Math.max(viewportWidth - left, right - left, 1),
      height: Math.max(bottom - top, 1),
    };
  }, PAGE_VIEWPORT_WIDTH_PX);

  return {
    x: Math.floor(clip.x),
    y: Math.floor(clip.y),
    width: Math.ceil(clip.width),
    height: Math.ceil(clip.height),
  };
}

async function pngToEscPos(page, pngBuffer) {
  const raster = await page.evaluate(async ({ base64, width, threshold }) => {
    const img = new Image();
    img.src = `data:image/png;base64,${base64}`;
    await img.decode();

    const targetHeight = Math.max(1, Math.round(img.height * (width / img.width)));
    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = targetHeight;

    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    ctx.fillStyle = '#fff';
    ctx.fillRect(0, 0, width, targetHeight);
    ctx.drawImage(img, 0, 0, width, targetHeight);

    const pixels = ctx.getImageData(0, 0, width, targetHeight).data;
    const bytesPerRow = Math.ceil(width / 8);
    const bytes = new Uint8Array(bytesPerRow * targetHeight);

    for (let y = 0; y < targetHeight; y += 1) {
      const rowOffset = y * bytesPerRow;
      for (let x = 0; x < width; x += 1) {
        const i = (y * width + x) * 4;
        const alpha = pixels[i + 3] / 255;
        const lum = (0.299 * pixels[i] + 0.587 * pixels[i + 1] + 0.114 * pixels[i + 2]) * alpha
          + 255 * (1 - alpha);
        if (lum < threshold) bytes[rowOffset + Math.floor(x / 8)] |= 0x80 >> (x % 8);
      }
    }

    return {
      bytesPerRow,
      height: targetHeight,
      rasterBase64: uint8ToBase64(bytes),
    };

    function uint8ToBase64(bytes) {
      let binary = '';
      const chunk = 0x8000;
      for (let i = 0; i < bytes.length; i += chunk) {
        binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
      }
      return btoa(binary);
    }
  }, {
    base64: pngBuffer.toString('base64'),
    width: PRINTER_WIDTH_PX,
    threshold: LUMINANCE_THRESHOLD,
  });

  return buildEscPosRasterPayload(
    Buffer.from(raster.rasterBase64, 'base64'),
    raster.bytesPerRow,
    raster.height);
}

function buildEscPosRasterPayload(raster, bytesPerRow, height) {
  const chunks = [Buffer.from([0x1B, 0x40, 0x1B, 0x61, 0x01])];
  const xL = bytesPerRow & 0xFF;
  const xH = (bytesPerRow >> 8) & 0xFF;

  for (let rowStart = 0; rowStart < height; rowStart += RASTER_CHUNK_ROWS) {
    const rows = Math.min(RASTER_CHUNK_ROWS, height - rowStart);
    chunks.push(Buffer.from([
      0x1D, 0x76, 0x30, 0x00,
      xL, xH,
      rows & 0xFF,
      (rows >> 8) & 0xFF,
    ]));
    chunks.push(raster.subarray(rowStart * bytesPerRow, (rowStart + rows) * bytesPerRow));
  }

  chunks.push(Buffer.concat([
    Buffer.alloc(LINE_BREAKS_BEFORE_CUT, 0x0A),
    Buffer.from([0x1D, 0x56, 0x00]),
  ]));
  return Buffer.concat(chunks);
}

function printUsb(pdfBuffer, printerName, copies) {
  if (!printerName) throw new Error('WindowsPrinterName not configured');

  const sumatraExe = path.join(__dirname, 'SumatraPDF.exe');
  if (!fs.existsSync(sumatraExe)) throw new Error(`SumatraPDF.exe not found at ${sumatraExe}`);

  const tmpFile = path.join(os.tmpdir(), `receipt_${Date.now()}_${process.pid}.pdf`);
  fs.writeFileSync(tmpFile, pdfBuffer);

  try {
    for (let i = 0; i < copies; i += 1) {
      const result = spawnSync(sumatraExe, [
        '-print-to',
        printerName,
        '-print-settings',
        'noscale',
        '-silent',
        tmpFile,
      ], {
        encoding: 'utf8',
        windowsHide: true,
      });

      if (result.error) throw result.error;
      if (result.status !== 0) {
        throw new Error(result.stderr || `SumatraPDF exited with code ${result.status}`);
      }
    }
  } finally {
    try { fs.unlinkSync(tmpFile); } catch { }
  }
}

async function printNetwork(pdfBuffer, printerIp, printerPort, copies) {
  if (!printerIp) throw new Error('IpAddress not configured');

  for (let i = 0; i < copies; i += 1) {
    await writeTcp(pdfBuffer, printerIp, printerPort || 9100);
  }
}

function writeTcp(buffer, host, port) {
  return new Promise((resolve, reject) => {
    const socket = new net.Socket();
    let settled = false;

    const done = (error) => {
      if (settled) return;
      settled = true;
      socket.destroy();
      if (error) reject(error);
      else resolve();
    };

    socket.setTimeout(NETWORK_TIMEOUT_MS, () => {
      done(new Error(`TCP write to ${host}:${port} timed out after ${NETWORK_TIMEOUT_MS}ms`));
    });

    socket.once('error', done);
    socket.connect(port, host, async () => {
      try {
        await writeChunked(socket, buffer);
        await delay(TCP_POST_WRITE_HOLD_MS);
        socket.end();
      } catch (error) {
        done(error);
      }
    });

    socket.once('close', () => done());
  });
}

async function writeChunked(socket, buffer) {
  for (let offset = 0; offset < buffer.length; offset += TCP_CHUNK_SIZE) {
    const end = Math.min(offset + TCP_CHUNK_SIZE, buffer.length);
    await socketWrite(socket, buffer.subarray(offset, end));
    if (end < buffer.length) await delay(TCP_CHUNK_DELAY_MS);
  }
}

function socketWrite(socket, chunk) {
  return new Promise((resolve, reject) => {
    socket.write(chunk, (error) => {
      if (error) reject(error);
      else resolve();
    });
  });
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function writeResult(result) {
  process.stdout.write(JSON.stringify(result));
}

main().catch((error) => {
  writeResult({ success: false, error: error && error.message ? error.message : String(error) });
});
