`#include "lib/json2.min.js"`

class Baum
  @version = '0.0.5'
  @maxLength = 1334

  run: ->
    @saveFolder = null
    if app.documents.length == 0
      filePaths = File.openDialog("Select a file", "*", true)
      for filePath in filePaths
        app.activeDocument = app.open(File(filePath))
        @runOneFile(true)

    else
      @runOneFile(false)

    alert('complete!')


  runOneFile: (after_close) =>
    @saveFolder = Folder.selectDialog("保存先フォルダの選択") if @saveFolder == null
    return if @saveFolder == null

    @documentName = app.activeDocument.name[0..-5]

    copiedDoc = app.activeDocument.duplicate(app.activeDocument.name[..-5] + '.copy.psd')
    @resizePsd(copiedDoc)
    @rasterizeAll(copiedDoc)
    @selectDocumentArea(copiedDoc)
    @ungroupArtboard(copiedDoc)
    @clipping(copiedDoc, copiedDoc)
    copiedDoc.selection.deselect()
    @psdToJson(copiedDoc)
    @psdToImage(copiedDoc)
    copiedDoc.close(SaveOptions.DONOTSAVECHANGES)
    app.activeDocument.close(SaveOptions.DONOTSAVECHANGES) if after_close


  selectDocumentArea: (document) ->
    x1 = 0
    y1 = 0
    x2 = document.width.value
    y2 = document.height.value
    selReg = [[x1,y1],[x2,y1],[x2,y2],[x1,y2]]
    document.selection.select(selReg)


  clipping: (document, root) ->
    if document.selection.bounds[0].value == 0 && document.selection.bounds[1].value == 0 && document.selection.bounds[2].value == document.width.value && document.selection.bounds[3].value == document.height.value
      return
    document.selection.invert()
    @clearAll(document, root)
    document.selection.invert()
    x1 = document.selection.bounds[0]
    y1 = document.selection.bounds[1]
    x2 = document.selection.bounds[2]
    y2 = document.selection.bounds[3]
    document.resizeCanvas(x2,y2,AnchorPosition.TOPLEFT)
    w = x2 - x1
    h = y2 - y1
    activeDocument.resizeCanvas(w,h,AnchorPosition.BOTTOMRIGHT)


  clearAll: (document, root) ->
    for layer in root.layers
      if layer.typename == 'LayerSet'
        @clearAll(document, layer)
      else if layer.typename == 'ArtLayer'
        if layer.kind != LayerKind.TEXT
          document.activeLayer = layer
          document.selection.clear()
      else
        alert(layer)


  resizePsd: (doc) ->
    width = doc.width
    height = doc.height
    return if width < Baum.maxLength && height < Baum.maxLength
    tmp = 0

    if width > height
      tmp = width / Baum.maxLength
    else
      tmp = height / Baum.maxLength

    width = width / tmp
    height = height / tmp
    doc.resizeImage(width, height, doc.resolution, ResampleMethod.NEARESTNEIGHBOR)


  rasterizeAll: (root) ->
    removeLayers = []
    for layer in root.layers
      if layer.visible == false
        removeLayers.push(layer)
      if layer.typename == 'LayerSet'
        @rasterizeAll(layer)
      else if layer.typename == 'ArtLayer'
        if layer.kind != LayerKind.TEXT
          layer.rasterize(RasterizeType.ENTIRELAYER)
      else
        alert(layer)

    if removeLayers.length > 0
      for i in [removeLayers.length-1..0]
        removeLayers[i].remove()

    t = 0
    while(t < root.layers.length)
      if root.layers[t].visible && root.layers[t].grouped
        root.layers[t].merge()
      else
        t += 1


  ungroupArtboard: (document) ->
    for layer in document.layers
      if layer.name.startsWith('Artboard') && layer.typename == 'LayerSet'
        @ungroup(layer)


  ungroup: (root) ->
    layers = for layer in root.layers
      layer
    for i in [0...layers.length]
      layers[i].moveBefore(root)
    root.remove()


  psdToJson: (targetDocument) ->
    toJson = new PsdToJson()
    json = toJson.run(targetDocument, @documentName)
    Util.saveText(@saveFolder + "/" + @documentName + ".layout.txt", json)

  psdToImage: (targetDocument) ->
    toImage = new PsdToImage()
    json = toImage.run(targetDocument, @saveFolder, @documentName)



class PsdToJson
  run: (document, documentName) ->
    layers = @allLayers(document, document)
    imageSize = [document.width.value, document.height.value]
    canvasSize = [document.width.value, document.height.value]
    canvasBase = [document.width.value/2, document.height.value/2]

    canvasLayer = @findLayer(document, '#Canvas')
    if canvasLayer
      bounds = canvasLayer.bounds
      canvasSize = [bounds[2].value - bounds[0].value, bounds[3].value - bounds[1].value]
      canvasBase = [(bounds[2].value + bounds[0].value)/2, (bounds[3].value + bounds[1].value)/2]

    json = JSON.stringify({
      info: {
        version: Baum.version
        canvas: {
          image: {
            w: imageSize[0]
            h: imageSize[1]
          }
          size: {
            w: canvasSize[0]
            h: canvasSize[1]
          }
          base: {
            x: canvasBase[0]
            y: canvasBase[1]
          }
        }
      }
      root: {
        type: 'Group'
        name: documentName
        elements: layers
      }
    })
    json

  findLayer: (root, name) ->
    for layer in root.layers
      return layer if layer.name == name
    null


  allLayers: (document, root) ->
    layers = []
    for layer in root.layers when (layer.visible and (not layer.name.startsWith('#')))
      hash = null
      name = layer.name.split("@")[0]
      opt = @parseOption(layer.name.split("@")[1])
      if layer.typename == 'ArtLayer'
        hash = @layerToHash(document, name, opt, layer)
      else
        hash = @groupToHash(document, name, opt, layer)
      if hash
        hash['name'] = name
        layers.push(hash)
    layers


  parseOption: (text) ->
    return {} unless text
    opt = {}
    for optText in text.split(",")
      elements = optText.split("=")
      elements[1] = true if elements.length == 1
      opt[elements[0].toLowerCase()] = elements[1]
    return opt

  layerToHash: (document, name, opt, layer) ->
    document.activeLayer = layer
    hash = {}
    if layer.kind == LayerKind.TEXT
      text = layer.textItem

      transform = @getActiveLayerTransform()
      angle = @angleFromMatrix(transform.yy, transform.xy)
      angle = 0 if angle == -90 # 謎の回転対策

      vx = layer.bounds[0].value
      ww = layer.bounds[2].value - layer.bounds[0].value
      vh = layer.bounds[3].value - layer.bounds[1].value
      originalText = text.contents
      text.contents = "-"

      vy = layer.bounds[1].value
      layer.rotate(angle)

      align = ''
      textColor = 0x000000
      try
        align = text.justification.toString()[14..-1].toLowerCase()
        textColor = text.color.rgb.hexValue
      catch e
        align = 'left'

      hash = {
        type: 'Text'
        text: originalText
        font: text.font
        size: Math.round(@getTextSize())
        color: textColor
        align: align
        x: vx
        y: vy
        w: ww
        h: layer.bounds[3].value - layer.bounds[1].value
        vh: vh
        opacity: Math.round(layer.opacity * 10.0)/10.0
        angle: Math.round(angle * 100.0)/100.0
      }
    else
      hash = {
        type: 'Image'
        image: Util.layerToImageName(layer)
        x: layer.bounds[0].value
        y: layer.bounds[1].value
        w: layer.bounds[2].value - layer.bounds[0].value
        h: layer.bounds[3].value - layer.bounds[1].value
        opacity: Math.round(layer.opacity * 10.0)/10.0
      }
      hash['prefab'] = opt['prefab'] if opt['prefab']
      hash['background'] = true if opt['background']
    hash

  angleFromMatrix: (yy, xy) ->
    toDegs = 180/Math.PI
    return Math.atan2(yy, xy) * toDegs - 90

  getActiveLayerTransform: ->
    ref = new ActionReference()
    ref.putEnumerated( charIDToTypeID("Lyr "), charIDToTypeID("Ordn"), charIDToTypeID("Trgt") )
    desc = executeActionGet(ref).getObjectValue(stringIDToTypeID('textKey'))
    if (desc.hasKey(stringIDToTypeID('transform')))
      desc = desc.getObjectValue(stringIDToTypeID('transform'))
      xx = desc.getDouble(stringIDToTypeID('xx'))
      xy = desc.getDouble(stringIDToTypeID('xy'))
      yy = desc.getDouble(stringIDToTypeID('yy'))
      yx = desc.getDouble(stringIDToTypeID('yx'))
      return {xx: xx, xy: xy, yy: yy, yx: yx}
    return {xx: 0, xy: 0, yy: 0, yx: 0}


  getTextSize: ->
    ref = new ActionReference()
    ref.putEnumerated( charIDToTypeID("Lyr "), charIDToTypeID("Ordn"), charIDToTypeID("Trgt") )
    desc = executeActionGet(ref).getObjectValue(stringIDToTypeID('textKey'))
    textSize =  desc.getList(stringIDToTypeID('textStyleRange')).getObjectValue(0).getObjectValue(stringIDToTypeID('textStyle')).getDouble (stringIDToTypeID('size'))
    if (desc.hasKey(stringIDToTypeID('transform')))
      mFactor = desc.getObjectValue(stringIDToTypeID('transform')).getUnitDoubleValue (stringIDToTypeID("yy") )
      textSize = (textSize* mFactor).toFixed(2)
    return textSize


  groupToHash: (document, name, opt, layer) ->
    hash = {}
    if name.endsWith('Button')
      hash = { type: 'Button' }
    else if name.endsWith('List')
      hash = { type: 'List' }
      hash['scroll'] = opt['scroll'] if opt['scroll']
    else if name.endsWith('Slider')
      hash = { type: 'Slider' }
    else
      hash = { type: 'Group' }
    hash['pivot'] = opt['pivot'] if opt['pivot']
    hash['elements'] = @allLayers(document, layer)
    hash


class PsdToImage
  baseFolder = null

  run: (document, saveFolder, documentName) ->
    @baseFolder = Folder(saveFolder + "/" + documentName)
    if @baseFolder.exists
      removeFiles = @baseFolder.getFiles()
      for i in [0...removeFiles.length]
        if removeFiles[i].name.startsWith(documentName) && removeFiles[i].name.endsWith('.png')
          removeFiles[i].remove()
      @baseFolder.remove()
    @baseFolder.create()

    targets = @allLayers(document)
    snapShotId = Util.takeSnapshot(document)
    for target in targets
      target.visible = true
      @outputLayer(document, target)
      Util.revertToSnapshot(document, snapShotId)


  allLayers: (root) ->
    for layer in root.layers when (layer.name.startsWith('#') or layer.kind == LayerKind.TEXT)
      layer.visible = false

    list = for layer in root.layers when (layer.visible and (not layer.name.startsWith('#')))
      if layer.typename == 'ArtLayer'
        layer.visible = false
        layer
      else
        @allLayers(layer)

    Array.prototype.concat.apply([], list) # list.flatten()


  outputLayer: (doc, layer) ->
    if !layer.isBackgroundLayer
      layer.translate(-layer.bounds[0], -layer.bounds[1])
      doc.resizeCanvas(layer.bounds[2] - layer.bounds[0], layer.bounds[3] - layer.bounds[1], AnchorPosition.TOPLEFT)
      doc.trim(TrimType.TRANSPARENT)

    layer.opacity = 100.0
    saveFile = new File("#{@baseFolder.fsName}/#{Util.layerToImageName(layer)}.png")
    options = new ExportOptionsSaveForWeb()
    options.format = SaveDocumentType.PNG
    options.PNG8 = false
    options.optimized = true
    options.interlaced = false
    doc.exportDocument(saveFile, ExportType.SAVEFORWEB, options)


class Util
  @saveText: (filePath, text) ->
    file = File(filePath)
    file.encoding = "UTF8"
    file.open("w", "TEXT")
    file.write(text)
    file.close()

  @layerToImageName: (layer) ->
    return layer.name.replace('.copy.psd', '').replace('.psd', '') if layer instanceof Document
    image = Util.layerToImageName(layer.parent)
    image + "_" + layer.name.split("@")[0].replace('_', '').replace(' ', '-')

  @getLastSnapshotID: (doc) ->
    hsObj = doc.historyStates
    hsLength = hsObj.length
    for i in [hsLength-1 .. -1]
      if hsObj[i].snapshot
        return i

  @takeSnapshot: (doc) ->
    desc153 = new ActionDescriptor()
    ref119 = new ActionReference()
    ref119.putClass(charIDToTypeID("SnpS"))
    desc153.putReference(charIDToTypeID("null"), ref119 )
    ref120 = new ActionReference()
    ref120.putProperty(charIDToTypeID("HstS"), charIDToTypeID("CrnH") )
    desc153.putReference(charIDToTypeID("From"), ref120 )
    executeAction(charIDToTypeID("Mk  "), desc153, DialogModes.NO )
    return Util.getLastSnapshotID(doc)

  @revertToSnapshot: (doc, snapshotID) ->
    doc.activeHistoryState = doc.historyStates[snapshotID]

String.prototype.startsWith = (str) ->
  return this.slice(0, str.length) == str

String.prototype.endsWith = (suffix) ->
  return this.indexOf(suffix, this.length - suffix.length) != -1

setup = ->
  preferences.rulerUnits = Units.PIXELS

setup()
baum = new Baum()
baum.run()
