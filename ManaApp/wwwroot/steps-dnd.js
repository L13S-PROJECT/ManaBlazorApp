window.stepsDnd = {
  allowMove: function (e) {
    try {
      e.dataTransfer.effectAllowed = "move";
      e.dataTransfer.dropEffect = "move";
      e.dataTransfer.setData("text/plain", "move");
    } catch {}
  }
};