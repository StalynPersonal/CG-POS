// Publicidad del segundo monitor.
//
// Un navegador solo reproduce un video sin que nadie lo toque si va sin sonido, y no basta con el atributo: hay que
// ponérselo a la propiedad y pedir la reproducción, porque el elemento lo crea Blazor después de cargar la página.
window.cgPantallaCliente = {
    reproducir: function (video) {
        if (!video) {
            return;
        }

        video.muted = true;
        video.defaultMuted = true;

        // Si el navegador la rechaza igual (pestaña en segundo plano, política del equipo), el video se queda en su
        // primer cuadro y el carrusel sigue su camino: la pantalla nunca se queda trabada por esto.
        const intento = video.play();
        if (intento && typeof intento.catch === 'function') {
            intento.catch(function () { });
        }
    },
};
