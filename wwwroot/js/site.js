// Beim Auswählen einer Datei -> zugehöriges Form submitten
// Upload-Vorprüfung: existiert im Ordner schon eine Datei mit gleichem Namen?
addEventListener('change', async (e) => {
    const input = e.target;
    if (!(input.matches('form#upload-form input[type="file"]'))) return;
    if (!input.files || input.files.length === 0) return;

    const form = input.closest('form#upload-form');
    const objectId = form.querySelector('input[name="objectId"]').value;
    const versioningField = form.querySelector('input[name="versioning"]');
    const nameOverrideField = form.querySelector('input[name="fileNameOverride"]');

    versioningField.value = "";
    nameOverrideField.value = "";

    const file = input.files[0];
    const fileName = file.name;

    try {
        const url = `/Files/Exists?objectId=${encodeURIComponent(objectId)}&fileName=${encodeURIComponent(fileName)}`;
        const res = await fetch(url, { method: 'GET', headers: { 'Accept': 'application/json' } });
        const data = await res.json();

        if (data.exists) {
            // Nachfrage: neue Version?
            const msg = `Die Datei "${fileName}" existiert bereits (aktuelle Version: v${data.version}).\n`
                + `Möchtest du eine NEUE VERSION erstellen?`;
            if (confirm(msg)) {
                versioningField.value = "true";   // Server versioniert dann automatisch
            } else {
                // Anderen Namen vorschlagen
                const dot = fileName.lastIndexOf('.');
                const base = dot > 0 ? fileName.substring(0, dot) : fileName;
                const ext = dot > 0 ? fileName.substring(dot) : '';
                const suggested = `${base} (neu)${ext}`;
                let newName = prompt("Unter welchem neuen Namen speichern?", suggested);
                if (!newName) {
                    // Abbruch = nichts hochladen
                    input.value = "";
                    return;
                }
                nameOverrideField.value = newName;
            }
        }
    } catch (err) {
        console.warn('Exists-Check fehlgeschlagen:', err);
        // Im Zweifel weiter hochladen – Server entscheidet dann
    }

    // Optional: Formular auto-submitten, ansonsten Nutzer klickt "Hochladen"
    // form.submit();
}, true);
// Fokus lösen, bevor Bootstrap aria-hidden setzt (verhindert die Warnung)
document.addEventListener('hide.bs.modal', (e) => {
    const modal = e.target;
    const active = document.activeElement;
    if (active && modal.contains(active)) {
        active.blur();            // Fokus aus dem Modal nehmen
    }
});

// Optional: Fokus zurück auf den Link/Knopf, der das Modal geöffnet hat
document.addEventListener('hidden.bs.modal', (e) => {
    const modal = e.target;
    const trigger = document.querySelector(`[data-bs-target="#${modal.id}"]`);
    if (trigger instanceof HTMLElement) trigger.focus();
});
