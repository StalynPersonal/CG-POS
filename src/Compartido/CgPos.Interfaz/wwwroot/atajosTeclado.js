// Captura F1–F12 y Escape para la pantalla POS.
// Bloquea el comportamiento del navegador (F5 recargar, F11 pantalla completa, F12 herramientas, etc.).
const TECLAS = /^(F([1-9]|1[0-2])|Escape)$/;
let manejador = null;

export function registrar(referenciaDotnet) {
    quitar();
    manejador = (evento) => {
        if (!TECLAS.test(evento.key)) return;
        evento.preventDefault();
        if (evento.repeat) return;
        referenciaDotnet.invokeMethodAsync("AlPresionarTecla", evento.key);
    };
    window.addEventListener("keydown", manejador, { capture: true });
}

export function quitar() {
    if (manejador) {
        window.removeEventListener("keydown", manejador, { capture: true });
        manejador = null;
    }
}
