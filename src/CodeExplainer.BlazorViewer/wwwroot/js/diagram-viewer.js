let mermaidModule;
const panViewports = new WeakSet();

async function loadMermaid() {
    mermaidModule ??= import("https://cdn.jsdelivr.net/npm/mermaid@11.4.1/dist/mermaid.esm.min.mjs");
    const module = await mermaidModule;
    module.default.initialize({
        startOnLoad: false,
        securityLevel: "strict",
        theme: "neutral",
        flowchart: { htmlLabels: false, curve: "basis" },
        sequence: { useMaxWidth: false, wrap: true },
    });
    return module.default;
}

export async function renderMermaid(elementId, source) {
    const element = document.getElementById(elementId);
    if (!element)
        return "The diagram canvas is unavailable.";

    try {
        const mermaid = await loadMermaid();
        const diagramId = `codeexplainer-${crypto.randomUUID()}`;
        const rendered = await mermaid.render(diagramId, source);
        element.replaceChildren();
        element.insertAdjacentHTML("afterbegin", rendered.svg);

        const svg = element.querySelector("svg");
        if (svg) {
            svg.setAttribute("role", "img");
            svg.setAttribute("aria-label", "Rendered project diagram");
            svg.style.display = "block";
            svg.style.maxWidth = "none";
            svg.dataset.baseWidth = String(svg.viewBox?.baseVal.width || 960);
            svg.dataset.baseHeight = String(svg.viewBox?.baseVal.height || 600);
            setDiagramZoom(elementId, 1);
        }
        return "";
    } catch (error) {
        element.replaceChildren();
        return `This Mermaid diagram could not be rendered: ${error instanceof Error ? error.message : String(error)}. Open “View diagram source” below to inspect it.`;
    }
}

export function fitDiagram(elementId) {
    const element = document.getElementById(elementId);
    const svg = element?.querySelector("svg");
    if (!svg)
        return;

    const baseWidth = Number(svg.dataset.baseWidth) || 960;
    const availableWidth = Math.max(320, element.parentElement?.clientWidth || baseWidth);
    const width = Math.min(baseWidth, availableWidth);
    svg.style.width = `${width}px`;
    svg.style.height = "auto";
    svg.dataset.zoom = String(width / baseWidth);
}

export function setDiagramZoom(elementId, factor) {
    const element = document.getElementById(elementId);
    const svg = element?.querySelector("svg");
    if (!svg)
        return;

    const baseWidth = Number(svg.dataset.baseWidth) || 960;
    const currentZoom = Number(svg.dataset.zoom) || 1;
    const zoom = Math.min(3.5, Math.max(0.05, currentZoom * factor));
    svg.style.width = `${Math.max(320, baseWidth * zoom)}px`;
    svg.style.height = "auto";
    svg.dataset.zoom = String(zoom);
}

export function setDiagramPanEnabled(elementId, enabled) {
    const viewport = document.getElementById(elementId)?.parentElement;
    if (!viewport)
        return;

    viewport.dataset.panEnabled = String(enabled);
    viewport.classList.toggle("pan-enabled", enabled);
    if (panViewports.has(viewport))
        return;

    panViewports.add(viewport);
    let pointerId = null;
    let lastX = 0;
    let lastY = 0;

    viewport.addEventListener("pointerdown", event => {
        if (viewport.dataset.panEnabled !== "true" || event.button !== 0)
            return;

        pointerId = event.pointerId;
        lastX = event.clientX;
        lastY = event.clientY;
        viewport.setPointerCapture(pointerId);
        viewport.classList.add("panning");
        event.preventDefault();
    });

    viewport.addEventListener("pointermove", event => {
        if (pointerId !== event.pointerId)
            return;

        viewport.scrollLeft -= event.clientX - lastX;
        viewport.scrollTop -= event.clientY - lastY;
        lastX = event.clientX;
        lastY = event.clientY;
    });

    const stopPanning = event => {
        if (pointerId !== event.pointerId)
            return;

        pointerId = null;
        viewport.classList.remove("panning");
    };

    viewport.addEventListener("pointerup", stopPanning);
    viewport.addEventListener("pointercancel", stopPanning);
    viewport.addEventListener("lostpointercapture", stopPanning);
}

export function setImageZoom(elementId, factor) {
    withLoadedImage(elementId, image => {
        const currentZoom = Number(image.dataset.zoom) || 1;
        setImageScale(image, currentZoom * factor);
    });
}

export function initializeImageView(elementId, svgSource = null) {
    const image = document.getElementById(elementId);
    if (!image)
        return;

    let dimensions = null;
    if (svgSource) {
        const parsed = new DOMParser().parseFromString(svgSource, "image/svg+xml");
        const root = parsed.documentElement;
        const values = (root.getAttribute("viewBox") || "").trim().split(/[,\s]+/).map(Number);
        if (root.localName !== "svg" || parsed.querySelector("parsererror") || values.length !== 4 ||
            !Number.isFinite(values[2]) || !Number.isFinite(values[3]) || values[2] <= 0 || values[3] <= 0) {
            throw new Error("The SVG has no valid viewBox dimensions.");
        }
        dimensions = { width: values[2], height: values[3] };
    }

    withLoadedImage(elementId, loadedImage => {
        const width = dimensions?.width || loadedImage.naturalWidth;
        const height = dimensions?.height || loadedImage.naturalHeight;
        if (!width || !height)
            return;

        loadedImage.dataset.baseWidth = String(width);
        loadedImage.dataset.baseHeight = String(height);
        setImageScale(loadedImage, 1);
    });
}

export function fitImage(elementId) {
    withLoadedImage(elementId, image => {
        const baseWidth = Number(image.dataset.baseWidth) || image.naturalWidth;
        const viewportWidth = image.parentElement?.clientWidth || baseWidth;
        const zoom = Math.min(1, viewportWidth / baseWidth);
        setImageScale(image, zoom);
    });
}

function withLoadedImage(elementId, callback) {
    const image = document.getElementById(elementId);
    if (!image)
        return;

    if (image.complete && image.naturalWidth > 0) {
        callback(image);
        return;
    }

    image.addEventListener("load", () => callback(image), { once: true });
}

function setImageScale(image, zoom) {
    const scale = Math.min(5, Math.max(0.005, zoom));
    const baseWidth = Number(image.dataset.baseWidth) || image.naturalWidth;
    const baseHeight = Number(image.dataset.baseHeight) || image.naturalHeight;
    image.style.width = `${baseWidth * scale}px`;
    image.style.height = `${baseHeight * scale}px`;
    image.dataset.zoom = String(scale);
}

export async function copyText(value) {
    await navigator.clipboard.writeText(value);
}
