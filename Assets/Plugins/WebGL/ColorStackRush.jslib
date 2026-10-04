mergeInto(LibraryManager.library, {
  $CSRStorageInit: 0,
  $CSRStorageInit__deps: ['$FS', '$IDBFS'],
  $CSRStorageInit__postset: "Module['unityFileSystemInit'] = function () { FS.mkdir('/idbfs'); Module.__unityIdbfsMount = FS.mount(IDBFS, { autoPersist: false }, '/idbfs'); Module.addRunDependency('JS_FileSystem_Mount'); function failed(error) { console.error('[Color Stack Rush] Could not open saved progress:', error); if (Module['csrStorageError']) Module['csrStorageError']('IndexedDB load failed'); else if (Module['showBanner']) Module['showBanner']('Browser storage could not be opened. Reload to try again.', 'error'); } try { FS.syncfs(true, function (error) { if (error) { failed(error); return; } Module.removeRunDependency('JS_FileSystem_Mount'); }); } catch (error) { failed(error); } };",
  CSR_Persist__deps: ['$FS'],
  CSR_Persist: function (id, receiverPtr) {
    var receiver = UTF8ToString(receiverPtr);
    var finished = false;
    function complete(ok) {
      if (finished) return;
      finished = true;
      SendMessage(receiver, 'OnPersistCompleted', id + '|' + (ok ? '1' : '0'));
    }
    try {
      FS.syncfs(false, function (error) {
        if (error) console.error('[Color Stack Rush] Storage sync failed:', error);
        complete(!error);
      });
    } catch (error) {
      console.error('[Color Stack Rush] Storage unavailable:', error);
      complete(false);
    }
  },
  CSR_RegisterBrowser__deps: ['$WEBAudio', '$CSRStorageInit'],
  CSR_RegisterBrowser: function (receiverPtr) {
    var receiver = UTF8ToString(receiverPtr);
    window.colorStackRush = window.colorStackRush || {};
    window.colorStackRush.unlockAudio = function () {
      if (WEBAudio.audioContext) WEBAudio.audioContext.resume().catch(function () {});
      SendMessage(receiver, 'OnBrowserAudioUnlocked', '');
    };
    function hidden() {
      if (document.hidden) SendMessage(receiver, 'OnBrowserHidden', '');
    }
    document.addEventListener('visibilitychange', hidden);
    window.addEventListener('pagehide', function () { SendMessage(receiver, 'OnBrowserHidden', ''); });
  }
});
