#!/usr/bin/env node
// Data-driven SVG art generator for INIS.
// Reads the canonical catalogue (Inis.Core/Data/*.json) and emits one SVG per card
// and per territory tile into game/art/. Godot 4 imports SVG natively, so no external
// rasterizer is needed. Re-run after editing the data so art and rules never drift.
//
//   node tools/gen-art.mjs
//
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const OUT = resolve(ROOT, "game/art");

const PALETTE = {
  parchment: "#efe2c0", ink: "#2b2218", gold: "#c8a24a", slate: "#2f4f4f",
  action: "#8c2f2f", epic: "#3c3a6e", advantage: "#3f6b43", reference: "#5a5a5a",
};
const TERRAIN = {
  Plains: ["#bcd98a", "#9bbf63"], Forest: ["#5e8c4f", "#3f6b3a"],
  Mountain: ["#b8b8b8", "#8c8c8c"], Bog: ["#9c8a4e", "#6f5f33"],
  Coast: ["#88c7c9", "#4f9aa0"],
};
const TYPE_COLOR = { Action: PALETTE.action, EpicTale: PALETTE.epic, Advantage: PALETTE.advantage, Reference: PALETTE.reference };
const TYPE_LABEL = { Action: "ACTION", EpicTale: "EPIC TALE", Advantage: "ADVANTAGE", Reference: "REFERENCE" };

function loadJson(rel) {
  const raw = readFileSync(resolve(ROOT, rel), "utf8")
    .split("\n").filter((l) => !l.trim().startsWith("//")).join("\n")
    .replace(/,(\s*[\]}])/g, "$1");
  return JSON.parse(raw);
}
function esc(s) { return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;"); }

// Naive word-wrap into <tspan> lines.
function wrap(text, max, x, y, lh) {
  const words = String(text).split(/\s+/);
  const lines = []; let cur = "";
  for (const w of words) {
    if ((cur + " " + w).trim().length > max) { if (cur) lines.push(cur); cur = w; }
    else cur = (cur + " " + w).trim();
  }
  if (cur) lines.push(cur);
  return lines.map((l, i) => `<tspan x="${x}" y="${y + i * lh}">${esc(l)}</tspan>`).join("");
}

function write(rel, svg) {
  const p = resolve(OUT, rel);
  mkdirSync(dirname(p), { recursive: true });
  writeFileSync(p, svg);
}

function cardSvg(card) {
  const W = 300, H = 420, accent = TYPE_COLOR[card.type] ?? PALETTE.slate;
  const flag = card.verified ? "" :
    `<text x="${W - 16}" y="28" text-anchor="end" font-family="sans-serif" font-size="11" fill="${accent}" opacity="0.8">provisional</text>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">
  <rect width="${W}" height="${H}" rx="18" fill="${PALETTE.parchment}"/>
  <rect x="8" y="8" width="${W - 16}" height="${H - 16}" rx="13" fill="none" stroke="${accent}" stroke-width="6"/>
  <rect x="8" y="8" width="${W - 16}" height="56" rx="13" fill="${accent}"/>
  <text x="${W / 2}" y="45" text-anchor="middle" font-family="Georgia, serif" font-size="24" fill="${PALETTE.parchment}">${esc(card.name)}</text>
  <text x="20" y="28" font-family="sans-serif" font-size="12" letter-spacing="2" fill="${PALETTE.parchment}" opacity="0.85">${TYPE_LABEL[card.type] ?? ""}</text>
  ${flag}
  <g transform="translate(${W / 2} 175)" fill="none" stroke="${accent}" stroke-width="6" stroke-linecap="round" opacity="0.85">
    <path d="M0 -34 C34 -34 34 6 0 6 C-34 6 -34 -34 0 -34" opacity="0.25"/>
    <path d="M0 -22 C20 -22 20 8 0 8 C-20 8 -20 -22 0 -22"/>
    <circle cx="0" cy="-7" r="4" fill="${accent}"/>
  </g>
  <rect x="22" y="250" width="${W - 44}" height="${H - 280}" rx="10" fill="#ffffff" opacity="0.45"/>
  <text x="32" y="278" font-family="Georgia, serif" font-size="15" fill="${PALETTE.ink}">${wrap(card.text ?? "", 34, 32, 278, 21)}</text>
</svg>\n`;
}

function tileSvg(t) {
  const [c1, c2] = TERRAIN[t.terrain] ?? TERRAIN.Plains;
  const cx = 150, cy = 150, r = 138;
  const pts = Array.from({ length: 6 }, (_, i) => {
    const a = Math.PI / 180 * (60 * i - 90);
    return `${(cx + r * Math.cos(a)).toFixed(1)},${(cy + r * Math.sin(a)).toFixed(1)}`;
  }).join(" ");
  const sanc = t.startsWithSanctuary
    ? `<g transform="translate(150 120)"><polygon points="0,-18 16,10 -16,10" fill="${PALETTE.parchment}" stroke="${PALETTE.gold}" stroke-width="3"/></g>` : "";
  return `<svg xmlns="http://www.w3.org/2000/svg" width="300" height="300" viewBox="0 0 300 300">
  <defs><linearGradient id="g" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${c1}"/><stop offset="1" stop-color="${c2}"/></linearGradient></defs>
  <polygon points="${pts}" fill="url(#g)" stroke="${PALETTE.slate}" stroke-width="6" stroke-linejoin="round"/>
  ${sanc}
  <rect x="40" y="206" width="220" height="40" rx="10" fill="${PALETTE.slate}" opacity="0.82"/>
  <text x="150" y="233" text-anchor="middle" font-family="Georgia, serif" font-size="20" fill="${PALETTE.parchment}">${esc(t.name)}</text>
</svg>\n`;
}

const cards = loadJson("Inis.Core/Data/cards.json");
const tiles = loadJson("Inis.Core/Data/territories.json");
let n = 0;
for (const c of cards) { if (c.art) { write(c.art, cardSvg(c)); n++; } }
for (const t of tiles) { if (t.art) { write(t.art, tileSvg(t)); n++; } }
console.log(`Generated ${n} SVG assets into game/art/ (${cards.length} cards, ${tiles.length} tiles).`);
