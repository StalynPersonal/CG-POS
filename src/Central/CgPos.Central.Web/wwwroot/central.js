// Utilidades del Central Manager. Todo local: sin CDN ni librerías externas.
window.cgCentral = {
    // Descarga un archivo que la aplicación ya trajo del Central (Excel, PDF o el 607).
    descargarArchivo: function (nombre, tipoContenido, contenidoBase64) {
        const binario = atob(contenidoBase64);
        const bytes = new Uint8Array(binario.length);
        for (let i = 0; i < binario.length; i++) {
            bytes[i] = binario.charCodeAt(i);
        }

        const url = URL.createObjectURL(new Blob([bytes], { type: tipoContenido }));
        const enlace = document.createElement('a');
        enlace.href = url;
        enlace.download = nombre;
        document.body.appendChild(enlace);
        enlace.click();
        document.body.removeChild(enlace);
        URL.revokeObjectURL(url);
    }
};
