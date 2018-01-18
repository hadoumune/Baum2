// Generated by CoffeeScript 1.10.0
(function() {
  #include "lib/json2.min.js";
  var Baum, PsdToImage, PsdToJson, Util, baum, setup,
    bind = function(fn, me){ return function(){ return fn.apply(me, arguments); }; };

  Baum = (function() {
    function Baum() {
      this.runOneFile = bind(this.runOneFile, this);
    }

    Baum.version = '0.4.0';

    Baum.maxLength = 1334;

    Baum.prototype.run = function() {
      var filePath, filePaths, j, len;
      this.saveFolder = null;
      if (app.documents.length === 0) {
        filePaths = File.openDialog("Select a file", "*", true);
        for (j = 0, len = filePaths.length; j < len; j++) {
          filePath = filePaths[j];
          app.activeDocument = app.open(File(filePath));
          this.runOneFile(true);
        }
      } else {
        this.runOneFile(false);
      }
      return alert('complete!');
    };

    Baum.prototype.runOneFile = function(after_close) {
      var copiedDoc;
      if (this.saveFolder === null) {
        this.saveFolder = Folder.selectDialog("保存先フォルダの選択");
      }
      if (this.saveFolder === null) {
        return;
      }
      this.documentName = app.activeDocument.name.slice(0, -4);
      copiedDoc = app.activeDocument.duplicate(app.activeDocument.name.slice(0, -4) + '.copy.psd');
      this.removeLayers(copiedDoc);
      this.resizePsd(copiedDoc);
      this.rasterizeAll(copiedDoc);
      this.selectDocumentArea(copiedDoc);
      this.ungroupArtboard(copiedDoc);
      this.clipping(copiedDoc, copiedDoc);
      this.layerMaskToLayer(copiedDoc, copiedDoc);
      copiedDoc.selection.deselect();
      this.psdToJson(copiedDoc);
      this.psdToImage(copiedDoc);
      copiedDoc.close(SaveOptions.DONOTSAVECHANGES);
      if (after_close) {
        return app.activeDocument.close(SaveOptions.DONOTSAVECHANGES);
      }
    };

    Baum.prototype.selectDocumentArea = function(document) {
      var selReg, x1, x2, y1, y2;
      x1 = 0;
      y1 = 0;
      x2 = document.width.value;
      y2 = document.height.value;
      selReg = [[x1, y1], [x2, y1], [x2, y2], [x1, y2]];
      return document.selection.select(selReg);
    };

    Baum.prototype.clipping = function(document, root) {
      var h, w, x1, x2, y1, y2;
      if (document.selection.bounds[0].value === 0 && document.selection.bounds[1].value === 0 && document.selection.bounds[2].value === document.width.value && document.selection.bounds[3].value === document.height.value) {
        return;
      }
      document.selection.invert();
      this.clearAll(document, root);
      document.selection.invert();
      x1 = document.selection.bounds[0];
      y1 = document.selection.bounds[1];
      x2 = document.selection.bounds[2];
      y2 = document.selection.bounds[3];
      document.resizeCanvas(x2, y2, AnchorPosition.TOPLEFT);
      w = x2 - x1;
      h = y2 - y1;
      return activeDocument.resizeCanvas(w, h, AnchorPosition.BOTTOMRIGHT);
    };

    Baum.prototype.clearAll = function(document, root) {
      var j, layer, len, ref1, results;
      ref1 = root.layers;
      results = [];
      for (j = 0, len = ref1.length; j < len; j++) {
        layer = ref1[j];
        if (layer.typename === 'LayerSet') {
          results.push(this.clearAll(document, layer));
        } else if (layer.typename === 'ArtLayer') {
          if (layer.kind !== LayerKind.TEXT) {
            document.activeLayer = layer;
            results.push(document.selection.clear());
          } else {
            results.push(void 0);
          }
        } else {
          results.push(alert(layer));
        }
      }
      return results;
    };

    Baum.prototype.resizePsd = function(doc) {
      var height, tmp, width;
      width = doc.width;
      height = doc.height;
      if (width < Baum.maxLength && height < Baum.maxLength) {
        return;
      }
      tmp = 0;
      if (width > height) {
        tmp = width / Baum.maxLength;
      } else {
        tmp = height / Baum.maxLength;
      }
      width = width / tmp;
      height = height / tmp;
      return doc.resizeImage(width, height, doc.resolution, ResampleMethod.NEARESTNEIGHBOR);
    };

    Baum.prototype.removeLayers = function(root) {
      var i, j, k, layer, len, ref1, ref2, removeLayers, results;
      removeLayers = [];
      ref1 = root.layers;
      for (j = 0, len = ref1.length; j < len; j++) {
        layer = ref1[j];
        if (layer.visible === false) {
          removeLayers.push(layer);
          continue;
        }
        if (layer.bounds[0].value === 0 && layer.bounds[1].value === 0 && layer.bounds[2].value === 0 && layer.bounds[3].value === 0) {
          removeLayers.push(layer);
          continue;
        }
        if (layer.name.startsWith('#')) {
          removeLayers.push(layer);
          continue;
        }
        if (layer.typename === 'LayerSet') {
          this.removeLayers(layer);
        }
      }
      if (removeLayers.length > 0) {
        results = [];
        for (i = k = ref2 = removeLayers.length - 1; ref2 <= 0 ? k <= 0 : k >= 0; i = ref2 <= 0 ? ++k : --k) {
          results.push(removeLayers[i].remove());
        }
        return results;
      }
    };

    Baum.prototype.rasterizeAll = function(root) {
      var j, layer, len, ref1, results, t;
      ref1 = root.layers;
      for (j = 0, len = ref1.length; j < len; j++) {
        layer = ref1[j];
        if (layer.typename === 'LayerSet') {
          this.rasterizeAll(layer);
        } else if (layer.typename === 'ArtLayer') {
          if (layer.kind !== LayerKind.TEXT) {
            this.rasterize(layer);
          }
        } else {
          alert(layer);
        }
      }
      t = 0;
      results = [];
      while (t < root.layers.length) {
        if (root.layers[t].visible && root.layers[t].grouped) {
          results.push(root.layers[t].merge());
        } else {
          results.push(t += 1);
        }
      }
      return results;
    };

    Baum.prototype.rasterize = function(layer) {
      var desc5, idLyr, idOrdn, idTrgt, idWhat, idlayerStyle, idnull, idrasterizeItem, idrasterizeLayer, ref4, tmp;
      tmp = app.activeDocument.activeLayer;
      app.activeDocument.activeLayer = layer;
      idrasterizeLayer = stringIDToTypeID("rasterizeLayer");
      desc5 = new ActionDescriptor();
      idnull = charIDToTypeID("null");
      ref4 = new ActionReference();
      idLyr = charIDToTypeID("Lyr ");
      idOrdn = charIDToTypeID("Ordn");
      idTrgt = charIDToTypeID("Trgt");
      ref4.putEnumerated(idLyr, idOrdn, idTrgt);
      desc5.putReference(idnull, ref4);
      idWhat = charIDToTypeID("What");
      idrasterizeItem = stringIDToTypeID("rasterizeItem");
      idlayerStyle = stringIDToTypeID("layerStyle");
      desc5.putEnumerated(idWhat, idrasterizeItem, idlayerStyle);
      executeAction(idrasterizeLayer, desc5, DialogModes.NO);
      return app.activeDocument.activeLayer = tmp;
    };

    Baum.prototype.ungroupArtboard = function(document) {
      var j, layer, len, ref1, results;
      ref1 = document.layers;
      results = [];
      for (j = 0, len = ref1.length; j < len; j++) {
        layer = ref1[j];
        if (layer.name.startsWith('Artboard') && layer.typename === 'LayerSet') {
          results.push(this.ungroup(layer));
        } else {
          results.push(void 0);
        }
      }
      return results;
    };

    Baum.prototype.ungroup = function(root) {
      var i, j, layer, layers, ref1;
      layers = (function() {
        var j, len, ref1, results;
        ref1 = root.layers;
        results = [];
        for (j = 0, len = ref1.length; j < len; j++) {
          layer = ref1[j];
          results.push(layer);
        }
        return results;
      })();
      for (i = j = 0, ref1 = layers.length; 0 <= ref1 ? j < ref1 : j > ref1; i = 0 <= ref1 ? ++j : --j) {
        layers[i].moveBefore(root);
      }
      return root.remove();
    };

    Baum.prototype.layerMaskToLayer = function(document, root) {
      var black, j, layer, len, newLayer, ref1, results;
      ref1 = root.layers;
      results = [];
      for (j = 0, len = ref1.length; j < len; j++) {
        layer = ref1[j];
        if (layer.typename !== 'LayerSet') {
          continue;
        }
        if (Util.hasLayerMask(document, layer)) {
          newLayer = document.artLayers.add();
          newLayer.name = layer.name + "_LayerMask@Mask";
          newLayer.move(layer, ElementPlacement.PLACEATBEGINNING);
          document.selection.deselect();
          Util.selectLayerMask(document, layer);
          document.activeLayer = newLayer;
          black = new SolidColor();
          black.rgb.red = 0;
          black.rgb.green = 0;
          black.rgb.blue = 0;
          document.selection.fill(black);
          Util.deleteLayerMask(document, layer);
        }
        results.push(this.layerMaskToLayer(document, layer));
      }
      return results;
    };

    Baum.prototype.psdToJson = function(targetDocument) {
      var json, toJson;
      toJson = new PsdToJson();
      json = toJson.run(targetDocument, this.documentName);
      return Util.saveText(this.saveFolder + "/" + this.documentName + ".layout.txt", json);
    };

    Baum.prototype.psdToImage = function(targetDocument) {
      var json, toImage;
      toImage = new PsdToImage();
      return json = toImage.run(targetDocument, this.saveFolder, this.documentName);
    };

    return Baum;

  })();

  PsdToJson = (function() {
    function PsdToJson() {}

    PsdToJson.prototype.run = function(document, documentName) {
      var bounds, canvasBase, canvasLayer, canvasSize, imageSize, json, layers;
      layers = this.allLayers(document, document);
      imageSize = [document.width.value, document.height.value];
      canvasSize = [document.width.value, document.height.value];
      canvasBase = [document.width.value / 2, document.height.value / 2];
      canvasLayer = this.findLayer(document, '#Canvas');
      if (canvasLayer) {
        bounds = canvasLayer.bounds;
        canvasSize = [bounds[2].value - bounds[0].value, bounds[3].value - bounds[1].value];
        canvasBase = [(bounds[2].value + bounds[0].value) / 2, (bounds[3].value + bounds[1].value) / 2];
      }
      json = JSON.stringify({
        info: {
          version: Baum.version,
          canvas: {
            image: {
              w: imageSize[0],
              h: imageSize[1]
            },
            size: {
              w: canvasSize[0],
              h: canvasSize[1]
            },
            base: {
              x: canvasBase[0],
              y: canvasBase[1]
            }
          }
        },
        root: {
          type: 'Root',
          name: documentName,
          elements: layers
        }
      });
      return json;
    };

    PsdToJson.prototype.findLayer = function(root, name) {
      var j, layer, len, ref1;
      ref1 = root.layers;
      for (j = 0, len = ref1.length; j < len; j++) {
        layer = ref1[j];
        if (layer.name === name) {
          return layer;
        }
      }
      return null;
    };

    PsdToJson.prototype.allLayers = function(document, root) {
      var hash, j, layer, layers, len, name, opt, ref1;
      layers = [];
      ref1 = root.layers;
      for (j = 0, len = ref1.length; j < len; j++) {
        layer = ref1[j];
        if (!layer.visible) {
          continue;
        }
        hash = null;
        name = layer.name.split("@")[0];
        opt = this.parseOption(layer.name.split("@")[1]);
        if (layer.typename === 'ArtLayer') {
          hash = this.layerToHash(document, name, opt, layer);
        } else {
          hash = this.groupToHash(document, name, opt, layer);
        }
        if (hash) {
          hash['name'] = name;
          layers.push(hash);
        }
      }
      return layers;
    };

    PsdToJson.prototype.parseOption = function(text) {
      var elements, j, len, opt, optText, ref1;
      if (!text) {
        return {};
      }
      opt = {};
      ref1 = text.split(",");
      for (j = 0, len = ref1.length; j < len; j++) {
        optText = ref1[j];
        elements = optText.split("=");
        if (elements.length === 1) {
          elements[1] = true;
        }
        opt[elements[0].toLowerCase()] = elements[1];
      }
      return opt;
    };

    PsdToJson.prototype.layerToHash = function(document, name, opt, layer) {
      var align, e, hash, originalText, text, textColor, vh, vx, vy, ww;
      document.activeLayer = layer;
      hash = {};
      if (layer.kind === LayerKind.TEXT) {
        text = layer.textItem;
        vx = layer.bounds[0].value;
        ww = layer.bounds[2].value - layer.bounds[0].value;
        vh = layer.bounds[3].value - layer.bounds[1].value;
        originalText = text.contents.replace(/\r\n/g, '__CRLF__').replace(/\r/g, '__CRLF__').replace(/\n/g, '__CRLF__').replace(/__CRLF__/g, '\r\n');
        text.contents = "-";
        vy = layer.bounds[1].value - (layer.bounds[3].value - layer.bounds[1].value) / 2.0;
        align = '';
        textColor = 0x000000;
        try {
          align = text.justification.toString().slice(14).toLowerCase();
          textColor = text.color.rgb.hexValue;
        } catch (error) {
          e = error;
          align = 'left';
        }
        hash = {
          type: 'Text',
          text: originalText,
          font: text.font,
          size: parseFloat(this.getTextSize()),
          color: textColor,
          align: align,
          x: vx,
          y: vy,
          w: ww,
          h: layer.bounds[3].value - layer.bounds[1].value,
          vh: vh,
          opacity: Math.round(layer.opacity * 10.0) / 10.0
        };
        if (Util.hasStroke(document, layer)) {
          hash['strokeSize'] = Util.getStrokeSize(document, layer);
          hash['strokeColor'] = Util.getStrokeColor(document, layer).rgb.hexValue;
        }
      } else if (opt['mask']) {
        hash = {
          type: 'Mask',
          image: Util.layerToImageName(layer),
          x: layer.bounds[0].value,
          y: layer.bounds[1].value,
          w: layer.bounds[2].value - layer.bounds[0].value,
          h: layer.bounds[3].value - layer.bounds[1].value,
          opacity: Math.round(layer.opacity * 10.0) / 10.0
        };
      } else {
        hash = {
          type: 'Image',
          image: Util.layerToImageName(layer),
          x: layer.bounds[0].value,
          y: layer.bounds[1].value,
          w: layer.bounds[2].value - layer.bounds[0].value,
          h: layer.bounds[3].value - layer.bounds[1].value,
          opacity: Math.round(layer.opacity * 10.0) / 10.0
        };
        if (opt['prefab']) {
          hash['prefab'] = opt['prefab'];
        }
        if (opt['background']) {
          hash['background'] = true;
        }
      }
      return hash;
    };

    PsdToJson.prototype.angleFromMatrix = function(yy, xy) {
      var toDegs;
      toDegs = 180 / Math.PI;
      return Math.atan2(yy, xy) * toDegs - 90;
    };

    PsdToJson.prototype.getActiveLayerTransform = function() {
      var desc, ref, xx, xy, yx, yy;
      ref = new ActionReference();
      ref.putEnumerated(charIDToTypeID("Lyr "), charIDToTypeID("Ordn"), charIDToTypeID("Trgt"));
      desc = executeActionGet(ref).getObjectValue(stringIDToTypeID('textKey'));
      if (desc.hasKey(stringIDToTypeID('transform'))) {
        desc = desc.getObjectValue(stringIDToTypeID('transform'));
        xx = desc.getDouble(stringIDToTypeID('xx'));
        xy = desc.getDouble(stringIDToTypeID('xy'));
        yy = desc.getDouble(stringIDToTypeID('yy'));
        yx = desc.getDouble(stringIDToTypeID('yx'));
        return {
          xx: xx,
          xy: xy,
          yy: yy,
          yx: yx
        };
      }
      return {
        xx: 0,
        xy: 0,
        yy: 0,
        yx: 0
      };
    };

    PsdToJson.prototype.getTextSize = function() {
      var desc, mFactor, ref, textSize;
      ref = new ActionReference();
      ref.putEnumerated(charIDToTypeID("Lyr "), charIDToTypeID("Ordn"), charIDToTypeID("Trgt"));
      desc = executeActionGet(ref).getObjectValue(stringIDToTypeID('textKey'));
      textSize = desc.getList(stringIDToTypeID('textStyleRange')).getObjectValue(0).getObjectValue(stringIDToTypeID('textStyle')).getDouble(stringIDToTypeID('size'));
      if (desc.hasKey(stringIDToTypeID('transform'))) {
        mFactor = desc.getObjectValue(stringIDToTypeID('transform')).getUnitDoubleValue(stringIDToTypeID("yy"));
        textSize = (textSize * mFactor).toFixed(2);
      }
      return textSize;
    };

    PsdToJson.prototype.groupToHash = function(document, name, opt, layer) {
      var hash;
      hash = {};
      if (name.endsWith('Button')) {
        hash = {
          type: 'Button'
        };
      } else if (name.endsWith('List')) {
        hash = {
          type: 'List'
        };
        if (opt['scroll']) {
          hash['scroll'] = opt['scroll'];
        }
      } else if (name.endsWith('Slider')) {
        hash = {
          type: 'Slider'
        };
        if (opt['scroll']) {
          hash['scroll'] = opt['scroll'];
        }
      } else if (name.endsWith('Scrollbar')) {
        hash = {
          type: 'Scrollbar'
        };
        if (opt['scroll']) {
          hash['scroll'] = opt['scroll'];
        }
      } else {
        hash = {
          type: 'Group'
        };
      }
      if (opt['pivot']) {
        hash['pivot'] = opt['pivot'];
      }
      hash['elements'] = this.allLayers(document, layer);
      return hash;
    };

    return PsdToJson;

  })();

  PsdToImage = (function() {
    var baseFolder;

    function PsdToImage() {}

    baseFolder = null;

    PsdToImage.prototype.run = function(document, saveFolder, documentName) {
      var i, j, k, len, ref1, removeFiles, results, snapShotId, target, targets;
      this.baseFolder = Folder(saveFolder + "/" + documentName);
      if (this.baseFolder.exists) {
        removeFiles = this.baseFolder.getFiles();
        for (i = j = 0, ref1 = removeFiles.length; 0 <= ref1 ? j < ref1 : j > ref1; i = 0 <= ref1 ? ++j : --j) {
          if (removeFiles[i].name.startsWith(documentName) && removeFiles[i].name.endsWith('.png')) {
            removeFiles[i].remove();
          }
        }
        this.baseFolder.remove();
      }
      this.baseFolder.create();
      targets = this.allLayers(document);
      snapShotId = Util.takeSnapshot(document);
      results = [];
      for (k = 0, len = targets.length; k < len; k++) {
        target = targets[k];
        target.visible = true;
        this.outputLayer(document, target);
        results.push(Util.revertToSnapshot(document, snapShotId));
      }
      return results;
    };

    PsdToImage.prototype.allLayers = function(root) {
      var j, layer, len, list, ref1;
      ref1 = root.layers;
      for (j = 0, len = ref1.length; j < len; j++) {
        layer = ref1[j];
        if (layer.kind === LayerKind.TEXT) {
          layer.visible = false;
        }
      }
      list = (function() {
        var k, len1, ref2, results;
        ref2 = root.layers;
        results = [];
        for (k = 0, len1 = ref2.length; k < len1; k++) {
          layer = ref2[k];
          if (layer.visible) {
            if (layer.typename === 'ArtLayer') {
              layer.visible = false;
              results.push(layer);
            } else {
              results.push(this.allLayers(layer));
            }
          }
        }
        return results;
      }).call(this);
      return Array.prototype.concat.apply([], list);
    };

    PsdToImage.prototype.outputLayer = function(doc, layer) {
      var options, saveFile;
      if (!layer.isBackgroundLayer) {
        layer.translate(-layer.bounds[0], -layer.bounds[1]);
        doc.resizeCanvas(layer.bounds[2] - layer.bounds[0], layer.bounds[3] - layer.bounds[1], AnchorPosition.TOPLEFT);
        doc.trim(TrimType.TRANSPARENT);
      }
      layer.opacity = 100.0;
      saveFile = new File(this.baseFolder.fsName + "/" + (Util.layerToImageName(layer)) + ".png");
      options = new ExportOptionsSaveForWeb();
      options.format = SaveDocumentType.PNG;
      options.PNG8 = false;
      options.optimized = true;
      options.interlaced = false;
      return doc.exportDocument(saveFile, ExportType.SAVEFORWEB, options);
    };

    return PsdToImage;

  })();

  Util = (function() {
    function Util() {}

    Util.saveText = function(filePath, text) {
      var file;
      file = File(filePath);
      file.encoding = "UTF8";
      file.open("w", "TEXT");
      file.write(text);
      return file.close();
    };

    Util.layerToImageName = function(layer) {
      var image;
      if (layer instanceof Document) {
        return layer.name.replace('.copy.psd', '').replace('.psd', '');
      }
      image = Util.layerToImageName(layer.parent);
      return image + "_" + layer.name.split("@")[0].replace('_', '').replace(' ', '-');
    };

    Util.getLastSnapshotID = function(doc) {
      var hsLength, hsObj, i, j, ref1;
      hsObj = doc.historyStates;
      hsLength = hsObj.length;
      for (i = j = ref1 = hsLength - 1; ref1 <= -1 ? j <= -1 : j >= -1; i = ref1 <= -1 ? ++j : --j) {
        if (hsObj[i].snapshot) {
          return i;
        }
      }
    };

    Util.takeSnapshot = function(doc) {
      var desc153, ref119, ref120;
      desc153 = new ActionDescriptor();
      ref119 = new ActionReference();
      ref119.putClass(charIDToTypeID("SnpS"));
      desc153.putReference(charIDToTypeID("null"), ref119);
      ref120 = new ActionReference();
      ref120.putProperty(charIDToTypeID("HstS"), charIDToTypeID("CrnH"));
      desc153.putReference(charIDToTypeID("From"), ref120);
      executeAction(charIDToTypeID("Mk  "), desc153, DialogModes.NO);
      return Util.getLastSnapshotID(doc);
    };

    Util.revertToSnapshot = function(doc, snapshotID) {
      return doc.activeHistoryState = doc.historyStates[snapshotID];
    };

    Util.hasLayerMask = function(doc, layer) {
      var desc, e, hasLayerMask, keyUserMaskEnabled, ref;
      doc.activeLayer = layer;
      hasLayerMask = false;
      try {
        ref = new ActionReference();
        keyUserMaskEnabled = charIDToTypeID("UsrM");
        ref.putProperty(charIDToTypeID("Prpr"), keyUserMaskEnabled);
        ref.putEnumerated(charIDToTypeID("Lyr "), charIDToTypeID("Ordn"), charIDToTypeID("Trgt"));
        desc = executeActionGet(ref);
        hasLayerMask = desc.hasKey(keyUserMaskEnabled);
      } catch (error) {
        e = error;
        hasLayerMask = false;
      }
      return hasLayerMask;
    };

    Util.selectLayerMask = function(doc, layer) {
      var desc153, desc299, e, id759, id760, id761, id762, id763, id764, idChnl, idOrdn, idT, idTrgt, idfsel, idnull, idsetd, ref117, ref118, ref92;
      doc.activeLayer = layer;
      try {
        id759 = charIDToTypeID("slct");
        desc153 = new ActionDescriptor();
        id760 = charIDToTypeID("null");
        ref92 = new ActionReference();
        id761 = charIDToTypeID("Chnl");
        id762 = charIDToTypeID("Chnl");
        id763 = charIDToTypeID("Msk ");
        ref92.putEnumerated(id761, id762, id763);
        desc153.putReference(id760, ref92);
        id764 = charIDToTypeID("MkVs");
        desc153.putBoolean(id764, false);
        executeAction(id759, desc153, DialogModes.NO);
        idsetd = charIDToTypeID("setd");
        desc299 = new ActionDescriptor();
        idnull = charIDToTypeID("null");
        ref117 = new ActionReference();
        idChnl = charIDToTypeID("Chnl");
        idfsel = charIDToTypeID("fsel");
        ref117.putProperty(idChnl, idfsel);
        desc299.putReference(idnull, ref117);
        idT = charIDToTypeID("T   ");
        ref118 = new ActionReference();
        idChnl = charIDToTypeID("Chnl");
        idOrdn = charIDToTypeID("Ordn");
        idTrgt = charIDToTypeID("Trgt");
        ref118.putEnumerated(idChnl, idOrdn, idTrgt);
        desc299.putReference(idT, ref118);
        return executeAction(idsetd, desc299, DialogModes.NO);
      } catch (error) {
        e = error;
        return alert(e);
      }
    };

    Util.deleteLayerMask = function(doc, layer) {
      var desc6, e, idChnl, idDlt, idOrdn, idTrgt, idnull, ref5;
      doc.activeLayer = layer;
      try {
        idDlt = charIDToTypeID("Dlt ");
        desc6 = new ActionDescriptor();
        idnull = charIDToTypeID("null");
        ref5 = new ActionReference();
        idChnl = charIDToTypeID("Chnl");
        idOrdn = charIDToTypeID("Ordn");
        idTrgt = charIDToTypeID("Trgt");
        ref5.putEnumerated(idChnl, idOrdn, idTrgt);
        desc6.putReference(idnull, ref5);
        return executeAction(idDlt, desc6, DialogModes.NO);
      } catch (error) {
        e = error;
      }
    };

    Util.hasStroke = function(doc, layer) {
      var desc1, desc2, hasFX, hasStroke, ref, res;
      doc.activeLayer = layer;
      res = false;
      ref = new ActionReference();
      ref.putEnumerated(charIDToTypeID("Lyr "), charIDToTypeID("Ordn"), charIDToTypeID("Trgt"));
      hasFX = executeActionGet(ref).hasKey(stringIDToTypeID('layerEffects'));
      if (hasFX) {
        hasStroke = executeActionGet(ref).getObjectValue(stringIDToTypeID('layerEffects')).hasKey(stringIDToTypeID('frameFX'));
        if (hasStroke) {
          desc1 = executeActionGet(ref);
          desc2 = executeActionGet(ref).getObjectValue(stringIDToTypeID('layerEffects')).getObjectValue(stringIDToTypeID('frameFX'));
          if (desc1.getBoolean(stringIDToTypeID('layerFXVisible')) && desc2.getBoolean(stringIDToTypeID('enabled'))) {
            res = true;
          }
        }
      }
      return res;
    };

    Util.getStrokeSize = function(doc, layer) {
      var desc, ref;
      doc.activeLayer = layer;
      ref = new ActionReference();
      ref.putEnumerated(charIDToTypeID("Lyr "), charIDToTypeID("Ordn"), charIDToTypeID("Trgt"));
      desc = executeActionGet(ref).getObjectValue(stringIDToTypeID('layerEffects')).getObjectValue(stringIDToTypeID('frameFX'));
      return desc.getUnitDoubleValue(stringIDToTypeID('size'));
    };

    Util.getStrokeColor = function(doc, layer) {
      var desc, ref;
      doc.activeLayer = layer;
      ref = new ActionReference();
      ref.putEnumerated(charIDToTypeID("Lyr "), charIDToTypeID("Ordn"), charIDToTypeID("Trgt"));
      desc = executeActionGet(ref).getObjectValue(stringIDToTypeID('layerEffects')).getObjectValue(stringIDToTypeID('frameFX'));
      return Util.getColorFromDescriptor(desc.getObjectValue(stringIDToTypeID("color")), typeIDToCharID(desc.getClass(stringIDToTypeID("color"))));
    };

    Util.getColorFromDescriptor = function(colorDesc, keyClass) {
      var colorObject;
      colorObject = new SolidColor();
      if (keyClass === "Grsc") {
        colorObject.grey.grey = color.getDouble(charIDToTypeID('Gry '));
      }
      if (keyClass === "RGBC") {
        colorObject.rgb.red = colorDesc.getDouble(charIDToTypeID('Rd  '));
        colorObject.rgb.green = colorDesc.getDouble(charIDToTypeID('Grn '));
        colorObject.rgb.blue = colorDesc.getDouble(charIDToTypeID('Bl  '));
      }
      if (keyClass === "CMYC") {
        colorObject.cmyk.cyan = colorDesc.getDouble(charIDToTypeID('Cyn '));
        colorObject.cmyk.magenta = colorDesc.getDouble(charIDToTypeID('Mgnt'));
        colorObject.cmyk.yellow = colorDesc.getDouble(charIDToTypeID('Ylw '));
        colorObject.cmyk.black = colorDesc.getDouble(charIDToTypeID('Blck'));
      }
      if (keyClass === "LbCl") {
        colorObject.lab.l = colorDesc.getDouble(charIDToTypeID('Lmnc'));
        colorObject.lab.a = colorDesc.getDouble(charIDToTypeID('A   '));
        colorObject.lab.b = colorDesc.getDouble(charIDToTypeID('B   '));
      }
      return colorObject;
    };

    return Util;

  })();

  String.prototype.startsWith = function(str) {
    return this.slice(0, str.length) === str;
  };

  String.prototype.endsWith = function(suffix) {
    return this.indexOf(suffix, this.length - suffix.length) !== -1;
  };

  setup = function() {
    return preferences.rulerUnits = Units.PIXELS;
  };

  setup();

  baum = new Baum();

  baum.run();

}).call(this);
