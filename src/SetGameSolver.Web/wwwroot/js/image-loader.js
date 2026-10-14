// Lädt ein Foto (aus Kamera, Datei oder URL), dreht es gemäß EXIF, verkleinert es
// und stellt die RGBA-Pixel für die Erkennung in .NET bereit. Alles bleibt im Browser.

let lastPixels = null;

async function decode(blob, maxSide) {
    const bitmap = await createImageBitmap(blob, { imageOrientation: 'from-image' });
    const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
    const width = Math.max(1, Math.round(bitmap.width * scale));
    const height = Math.max(1, Math.round(bitmap.height * scale));

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(bitmap, 0, 0, width, height);
    bitmap.close?.();

    lastPixels = new Uint8Array(ctx.getImageData(0, 0, width, height).data.buffer);
    const url = canvas.toDataURL('image/jpeg', 0.85);
    return { width, height, url };
}

export async function loadFromInput(input, maxSide) {
    const file = input?.files?.[0];
    if (!file) return null;
    try {
        return await decode(file, maxSide);
    } finally {
        input.value = ''; // dasselbe Foto erneut wählbar machen
    }
}

export async function loadFromUrl(url, maxSide) {
    const response = await fetch(url);
    if (!response.ok) throw new Error(`Bild konnte nicht geladen werden: ${response.status}`);
    return await decode(await response.blob(), maxSide);
}

// Gibt die Pixel des zuletzt geladenen Bildes zurück (als byte[] in .NET).
export function takePixels() {
    const pixels = lastPixels;
    lastPixels = null;
    return pixels;
}

// Kurz dem Browser Zeit geben, die Oberfläche neu zu zeichnen, bevor gerechnet wird.
export function nextFrame() {
    return new Promise(resolve => requestAnimationFrame(() => setTimeout(resolve, 0)));
}
