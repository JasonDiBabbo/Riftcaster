// @ts-check
/// <reference types="node" />
/**
 * Makes the logo's bitmap icons from its SVG (src/Riftcaster.Admin/wwwroot/logo/icon.svg):
 *
 * - src/Riftcaster.Windows/Riftcaster.ico: the launcher's .exe and tray icon, 16 to 256 px.
 * - src/Riftcaster.Admin/wwwroot/logo/favicon.ico: the admin's browser tab icon, 16 to 48 px.
 * - src/Riftcaster.Admin/wwwroot/logo/apple-touch-icon.png: 180 px, for a phone or tablet's
 *   home screen. Square, since the phone rounds the corners itself.
 *
 * Microsoft Edge (or Chrome) draws each size from the SVG, so small sizes are drawn at that size
 * rather than shrunk from a big one, with a wider tear so it stays visible. The outputs are
 * committed; run this again after changing the SVG. Set BROWSER to the browser's path if it isn't
 * found. Run it from PowerShell or a command prompt: from Git Bash the browser prints nothing.
 *
 * Usage: node tools/logo/build-icons.mjs
 */
import { execFileSync } from "child_process";
import fs from "fs";
import os from "os";
import path from "path";
import zlib from "zlib";

const root = path.resolve(import.meta.dirname, "../..");
const logoDir = "src/Riftcaster.Admin/wwwroot/logo";
const icon = fs.readFileSync(path.join(root, logoDir, "icon.svg"), "utf8");
const square = icon.replace('rx="14" ', "");
// At 24 px and under the card is drawn bigger and its tear wider, or the tear blurs into a grey line.
const small = icon
  .replace('stroke-width="3.5"', 'stroke-width="5.5"')
  .replace("scale(0.8)", "scale(0.88)");
if (
  square === icon ||
  small.includes("scale(0.8)") ||
  small.includes('stroke-width="3.5"')
)
  throw new Error(
    "icon.svg changed: update the small and square versions here to match.",
  );

const windowsSizes = [16, 20, 24, 32, 40, 48, 64, 256];
const faviconSizes = [16, 32, 48];
const appleSize = 180;

const drawn = draw([
  ...windowsSizes.map((size) => ({
    name: `icon-${size}`,
    svg: size <= 24 ? small : icon,
    size,
  })),
  { name: "apple", svg: square, size: appleSize },
]);

const ico = (/** @type {number[]} */ sizes) =>
  makeIco(sizes.map((size) => drawn[`icon-${size}`]));
write("src/Riftcaster.Windows/Riftcaster.ico", ico(windowsSizes));
write(`${logoDir}/favicon.ico`, ico(faviconSizes));
write(`${logoDir}/apple-touch-icon.png`, drawn.apple);

/**
 * Draws each SVG at its size in a headless browser, returning PNGs by name.
 * @param {{ name: string, svg: string, size: number }[]} jobs
 * @returns {Record<string, Buffer>}
 */
function draw(jobs) {
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), "riftcaster-icons-"));
  try {
    const page = path.join(temp, "draw.html");
    fs.writeFileSync(
      page,
      `<pre id="out"></pre><script>
const jobs = ${JSON.stringify(jobs)};
Promise.all(jobs.map((job) => new Promise((resolve, reject) => {
  const image = new Image();
  image.onload = () => {
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = job.size;
    canvas.getContext('2d').drawImage(image, 0, 0, job.size, job.size);
    resolve([job.name, canvas.toDataURL('image/png').split(',')[1]]);
  };
  image.onerror = () => reject(new Error('Could not load ' + job.name));
  image.src = 'data:image/svg+xml;base64,' + btoa(job.svg);
}))).then((pngs) => { out.textContent = JSON.stringify(Object.fromEntries(pngs)); },
  (error) => { out.textContent = JSON.stringify({ error: String(error) }); });
</script>`,
    );
    const dom = execFileSync(
      findBrowser(),
      [
        "--headless",
        "--disable-gpu",
        `--user-data-dir=${path.join(temp, "profile")}`, // Leaves a running browser alone
        "--virtual-time-budget=10000",
        "--dump-dom",
        `file:///${page.replaceAll("\\", "/")}`,
      ],
      {
        encoding: "utf8",
        maxBuffer: 64 * 1024 * 1024,
        stdio: ["ignore", "pipe", "ignore"],
      },
    );
    const json = dom.match(/<pre id="out">(.*?)<\/pre>/s)?.[1];
    if (!json) throw new Error("The browser drew nothing.");
    const result = JSON.parse(json);
    if (result.error) throw new Error(result.error);
    return Object.fromEntries(
      Object.entries(result).map(([name, png]) => [
        name,
        Buffer.from(png, "base64"),
      ]),
    );
  } finally {
    fs.rmSync(temp, { recursive: true, force: true });
  }
}

function findBrowser() {
  const candidates = [
    process.env.BROWSER,
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    "C:/Program Files/Microsoft/Edge/Application/msedge.exe",
    "C:/Program Files/Google/Chrome/Application/chrome.exe",
  ];
  const found = candidates.find(
    (candidate) => candidate && fs.existsSync(candidate),
  );
  if (!found)
    throw new Error(
      "Microsoft Edge or Chrome is needed. Set BROWSER to its path.",
    );
  return found;
}

/**
 * An .ico holding each PNG. Sizes up to 64 px are stored as bitmaps, which everything that reads
 * .ico files understands; 256 px stays PNG, as Windows expects, to keep the file small.
 * @param {Buffer[]} pngs
 */
function makeIco(pngs) {
  const images = pngs.map((png) => {
    const { width, height } = readPng(png);
    return { width, height, data: width >= 256 ? png : toBitmap(readPng(png)) };
  });
  const header = Buffer.alloc(6 + 16 * images.length);
  header.writeUInt16LE(1, 2); // Type: icon
  header.writeUInt16LE(images.length, 4);
  let offset = header.length;
  images.forEach((image, i) => {
    const entry = 6 + 16 * i;
    header.writeUInt8(image.width % 256, entry); // 0 means 256
    header.writeUInt8(image.height % 256, entry + 1);
    header.writeUInt16LE(1, entry + 4); // Colour planes
    header.writeUInt16LE(32, entry + 6); // Bits per pixel
    header.writeUInt32LE(image.data.length, entry + 8);
    header.writeUInt32LE(offset, entry + 12);
    offset += image.data.length;
  });
  return Buffer.concat([header, ...images.map((image) => image.data)]);
}

/**
 * A 32-bit icon bitmap: a BITMAPINFOHEADER, the pixels bottom row first in BGRA, then an empty
 * AND mask (the alpha channel does the masking).
 * @param {{ width: number, height: number, rgba: Buffer }} image
 */
function toBitmap({ width, height, rgba }) {
  const info = Buffer.alloc(40);
  info.writeUInt32LE(40, 0);
  info.writeInt32LE(width, 4);
  info.writeInt32LE(height * 2, 8); // Pixels and mask together
  info.writeUInt16LE(1, 12);
  info.writeUInt16LE(32, 14);
  const pixels = Buffer.alloc(width * height * 4);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const from = (y * width + x) * 4;
      const to = ((height - 1 - y) * width + x) * 4;
      pixels[to] = rgba[from + 2];
      pixels[to + 1] = rgba[from + 1];
      pixels[to + 2] = rgba[from];
      pixels[to + 3] = rgba[from + 3];
    }
  }
  const mask = Buffer.alloc(Math.ceil(width / 32) * 4 * height);
  return Buffer.concat([info, pixels, mask]);
}

/**
 * Decodes an 8-bit RGBA, non-interlaced PNG, which is what a canvas writes.
 * @param {Buffer} png
 */
function readPng(png) {
  let pos = 8;
  let width = 0;
  let height = 0;
  const idat = [];
  while (pos < png.length) {
    const length = png.readUInt32BE(pos);
    const type = png.toString("ascii", pos + 4, pos + 8);
    const data = png.subarray(pos + 8, pos + 8 + length);
    if (type === "IHDR") {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      if (data[8] !== 8 || data[9] !== 6 || data[12] !== 0)
        throw new Error("Expected an 8-bit RGBA PNG.");
    } else if (type === "IDAT") {
      idat.push(data);
    }
    pos += 12 + length;
  }
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const stride = width * 4;
  const rgba = Buffer.alloc(stride * height);
  for (let y = 0; y < height; y++) {
    const filter = raw[y * (stride + 1)];
    const line = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    for (let x = 0; x < stride; x++) {
      const left = x >= 4 ? rgba[y * stride + x - 4] : 0;
      const up = y > 0 ? rgba[(y - 1) * stride + x] : 0;
      const upLeft = x >= 4 && y > 0 ? rgba[(y - 1) * stride + x - 4] : 0;
      let value = line[x];
      if (filter === 1) value += left;
      else if (filter === 2) value += up;
      else if (filter === 3) value += (left + up) >> 1;
      else if (filter === 4) value += paeth(left, up, upLeft);
      rgba[y * stride + x] = value & 0xff;
    }
  }
  return { width, height, rgba };
}

/** @param {number} a @param {number} b @param {number} c */
function paeth(a, b, c) {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
}

/** @param {string} file @param {Buffer} data */
function write(file, data) {
  fs.writeFileSync(path.join(root, file), data);
  console.log(`${file} (${data.length} bytes)`);
}
