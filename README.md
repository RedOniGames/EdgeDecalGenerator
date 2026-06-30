**EdgeDecalGenerator User Manual** 

**Overview**

EdgeDecalGenerator is a Unity tool designed for procedural generation of mesh decals along object edges. It is perfect for adding chips, dirt, dust, and wear-and-tear to the corners of buildings, 
crates, and various environment props.

**Installation**
1. Download the .unitypackage from the Releases section on GitHub (or other provided platforms).
2. Import the package into your Unity project (Assets -> Import Package -> Custom Package).
3. Ensure that the Editor folder is included in your project, as it manages the tool's interface and custom windows.

**Quick Start**
1. Select a Mesh object in your scene.
2. Open the tool window: Tools -> RedOniTools -> Edge Decal Master.
3. Assign a Decal Material (a sample material is included with the plugin).
4. Click Generate Edge Decals.

**Parameters Description**
- **Width:** Controls the width of the generated decal strip.
- **Z Offset:** The distance the decal sits above the surface (recommended values: 0.001–0.005 to prevent Z-fighting).
- **Hard Edge Angle:** The threshold angle between polygons. If the angle is greater than this value, the edge is considered "sharp," and a decal will be generated.
- **Lighting Mode:**
  - **Flat:** Provides uniform, flat lighting across the decal.
  - **Smooth:** Creates soft, interpolated shadows (simulating a rounded bevel effect).
  - **Hard:** Mimics original hard edges by copying the source mesh's normals (ideal for cubes and low-poly hard surface models).
- **Trim Count / Index:** Enables the use of Trim Atlases (multiple decal types packed into a single texture). You can select the desired strip by clicking directly on the texture preview image.

**Important (Saving Assets)**
By default, generated meshes are stored locally within the scene file. If you are planning to turn your object into a Prefab, make sure to check the Save Mesh As Asset box. 
This will save the mesh as a standalone file in the Assets/GeneratedEdges folder, ensuring the decal remains linked when the object is moved between scenes or instantiated.

-------------------------------------------------------------------------------------------------------------------------------------------------

**Инструкция по работе с EdgeDecalGenerator**

**Обзор**  
EdgeDecalGenerator — это инструмент для Unity, позволяющий процедурно создавать Mesh-декали на ребрах объектов. Идеально подходит для добавления сколов, грязи и износа на углы зданий, ящиков и элементов окружения.

**Установка**
1. Скачайте .unitypackage из раздела Releases на GitHub (или других площадок).    
2. Импортируйте пакет в проект Unity (Assets -> Import Package -> Custom Package).    
3. Убедитесь, что папка Editor находится внутри проекта — она отвечает за интерфейс.    

**Быстрый старт**
1. Выделите объект (Mesh) в сцене.    
2. Откройте окно мастера: Tools -> RedOniTools -> Edge Decal Master.    
3. Назначьте **Decal Material** (в комплекте идет тестовый).    
4. Нажмите **Generate Edge Decals**.    

**Описание параметров**
- **Width:** Ширина полоски декали.    
- **Z Offset:** Расстояние от поверхности (рекомендуется 0.001–0.005 для предотвращения Z-fighting).    
- **Hard Edge Angle:** Угол, при котором ребро считается «острым» и на нем создается декаль.    
- **Lighting Mode:**    
    - Flat: Равномерное освещение.        
    - Smooth: Плавные тени (эффект скругленного угла).        
    - Hard: Имитация жестких ребер (копирует освещение оригинала, идеально для кубов).        
- **Trim Count / Index:** Позволяет использовать Трим-атласы (одна текстура на много типов декалей). Выбирайте нужную полоску кликом прямо по превью текстуры.    

**Важно (Сохранение)**  
По умолчанию меши хранятся в сцене. Если вы создаете префаб, поставьте галочку **Save Mesh As Asset**. Меш сохранится в папку Assets/GeneratedEdges и не пропадет при переносе объекта.
