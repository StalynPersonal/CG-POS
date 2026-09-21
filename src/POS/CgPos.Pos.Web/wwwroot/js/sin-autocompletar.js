// La caja no guarda ni sugiere lo que se escribe: cada campo y formulario lleva autocomplete="off".
//
// Se aplica aquí, una sola vez para toda la caja, y no campo por campo: los diálogos, el teclado en pantalla y las
// búsquedas crean sus campos después de cargar la página, y un observador los alcanza a todos, también a los que MudBlazor
// vuelve a pintar. Ojo: Chrome y Edge ignoran esta marca en los campos de clave; ahí el guardado se apaga en el navegador.
(function () {
    const selector = 'input, textarea, select, form';

    function apagar(elemento) {
        if (elemento.getAttribute('autocomplete') !== 'off') {
            elemento.setAttribute('autocomplete', 'off');
        }
    }

    function recorrer(raiz) {
        if (raiz.nodeType !== Node.ELEMENT_NODE) {
            return;
        }
        if (raiz.matches(selector)) {
            apagar(raiz);
        }
        raiz.querySelectorAll(selector).forEach(apagar);
    }

    new MutationObserver(function (cambios) {
        for (const cambio of cambios) {
            if (cambio.type === 'attributes') {
                apagar(cambio.target);
            } else {
                cambio.addedNodes.forEach(recorrer);
            }
        }
    }).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['autocomplete'] });

    recorrer(document.documentElement);
})();
