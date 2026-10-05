import { readFile, mkdir } from "node:fs/promises";
import path from "node:path";
import sharp from "sharp";

const root = process.cwd();
const source = await readFile(path.join(root, "public", "active-cash-logo.svg"));
const sizes = { ldpi: 36, mdpi: 48, hdpi: 72, xhdpi: 96, xxhdpi: 144, xxxhdpi: 192 };

for (const [density, size] of Object.entries(sizes)) {
  const directory = path.join(root, "android", "app", "src", "main", "res", `mipmap-${density}`);
  await mkdir(directory, { recursive: true });
  const logo = sharp(source).resize(size, size);
  await logo.png().toFile(path.join(directory, "ic_launcher.png"));
  await sharp(source).resize(size, size).png().toFile(path.join(directory, "ic_launcher_round.png"));

  const foregroundSize = Math.round(size * .72);
  const inset = Math.floor((size - foregroundSize) / 2);
  const foreground = await sharp(source).resize(foregroundSize, foregroundSize).png().toBuffer();
  await sharp({ create: { width: size, height: size, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
    .composite([{ input: foreground, left: inset, top: inset }]).png().toFile(path.join(directory, "ic_launcher_foreground.png"));
  await sharp({ create: { width: size, height: size, channels: 4, background: "#08764d" } }).png().toFile(path.join(directory, "ic_launcher_background.png"));
}

