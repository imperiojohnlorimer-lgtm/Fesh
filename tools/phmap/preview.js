const fs = require("fs");
const src = fs.readFileSync(__dirname + "/../../fesh-csharp/src/PhMapData.cs", "utf8");
const cols = +src.match(/Cols = (\d+)/)[1], rows = +src.match(/Rows = (\d+)/)[1];
const lines = [...src.matchAll(/^        "([0-9sl]*)",$/gm)].map(m => m[1]);
const buf = Buffer.alloc(cols * rows * 3);
lines.forEach((ln, y) => { let x = 0; for (const [, n, t] of ln.matchAll(/(\d+)([sl])/g)) { for (let i = 0; i < +n; i++, x++) { const o = (y * cols + x) * 3; if (t === "l") { buf[o] = 90; buf[o+1] = 160; buf[o+2] = 80; } else { buf[o] = 30; buf[o+1] = 80; buf[o+2] = 140; } } } });
fs.writeFileSync(__dirname + "/preview.ppm", Buffer.concat([Buffer.from(`P6 ${cols} ${rows} 255\n`), buf]));
