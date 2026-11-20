# Taller 6 - Archivos Planos y Grafos: rutas de recogida y entrega

Programa de consola en **C# / .NET 9** que lee el mapa de una ciudad (`city.txt`) y la lista de
paquetes del día (`packages.txt`), y calcula en qué orden debe visitar los puntos un domiciliario
para recoger y entregar todos los paquetes recorriendo la menor distancia posible.

## Cómo ejecutar

```bash
dotnet build
dotnet run -- city.txt packages.txt D
```

- Argumentos: archivo del mapa, archivo de paquetes y posición inicial.
- Sin argumentos (`dotnet run`), el programa pide los tres datos por consola
  (con Enter se usan los valores por defecto `city.txt`, `packages.txt` y `D`).
- Opcional: `dotnet run -- city.txt packages.txt D --comparar` imprime al final una tabla con el
  total y el tiempo de la heurística voraz y de la heurística mejorada (reto opcional 4).
- El ejemplo pequeño: `dotnet run -- city_mini.txt packages_mini.txt A`.

## Estructura

```
DeliveryRoutes.csproj
Program.cs                      lee argumentos / consola, llama al backend e imprime
city.txt, packages.txt          datos del taller
city_mini.txt, packages_mini.txt  ejemplo pequeño de la sección 6
Backend/
  Edge.cs, Graph.cs             grafo ponderado con lista de adyacencia (dirigido o no)
  ShortestPaths.cs              Dijkstra con PriorityQueue y reconstrucción del camino
  FlatFile.cs                   lectura común de archivos planos (comentarios, líneas en blanco)
  CityFile.cs                   carga y valida city.txt (TwoWay = dos aristas, OneWay = una)
  Package.cs, PackageFile.cs    carga y valida packages.txt y agrupa las tareas por paquete
  Stop.cs                       una parada: ubicación + recoger/entregar + paquete
  RoutePlanner.cs               tabla de distancias, heurísticas, IsValid y TotalDistance
  RoutePrinter.cs               arma la salida de la sección 5
  InputValidationException.cs   errores de los datos (todos juntos, con número de línea)
```

## Validaciones

Antes de calcular nada se revisa todo y se muestran **todos** los errores encontrados, con su
número de línea:

- líneas con un número de campos distinto al esperado;
- distancias que no son un número positivo;
- sentidos que no son `TwoWay` ni `OneWay` (sin importar mayúsculas);
- calles repetidas (`A,B` y `B,A` son la misma calle) o que empiezan y terminan en el mismo punto;
- ubicaciones de paquetes o posición inicial que no existen en el mapa;
- acciones que no son `Pickup` ni `Delivery`, o números de paquete que no son enteros mayores que 0;
- paquetes con dos recogidas o dos entregas, o a los que les falta la recogida o la entrega;
- puntos no alcanzables desde la posición inicial, o puntos desde los que no se puede seguir
  hacia los demás (por ejemplo, un punto al que se entra por una calle de un solo sentido y del
  que no se puede salir).

## Estrategia para ordenar las paradas

### Por qué no se prueban todas las combinaciones

Con 30 paquetes hay 60 paradas. Aun respetando que cada paquete se recoja antes de entregarse,
hay 60! / 2^30 órdenes posibles: un número de 73 cifras. Ni revisando millones de rutas por
segundo durante la edad del universo se alcanzarían a probar todas. Este es el *problema de
recogida y entrega* (una variante del agente viajero) y no se conoce un algoritmo exacto y rápido
para tamaños grandes, así que se usa una **heurística**: un método que no garantiza el óptimo
pero encuentra rutas muy buenas en poco tiempo.

### Paso 1 (exacto): distancias con Dijkstra

El mapa se carga como grafo **dirigido**: cada calle de doble sentido son dos aristas y cada calle
de un solo sentido es una sola. No sirve BFS porque BFS encuentra el camino con **menos calles**,
no el de **menos metros**; Dijkstra sí tiene en cuenta el peso de cada calle.

Solo importan 61 puntos como máximo (la posición inicial y los puntos de recogida/entrega), así que
se corre Dijkstra **una vez desde cada uno** y se guarda una tabla `distancia[x][y]` más los
predecesores para reconstruir el camino. Como hay calles de un solo sentido, `distancia[x][y]` y
`distancia[y][x]` pueden ser distintas, y se guardan las dos. Desde ahí el resto del algoritmo
trabaja sobre la tabla, sin volver a tocar el grafo.

### Paso 2 (heurística): construir y mejorar la ruta

1. **Inserción más barata.** La ruta se arma paquete por paquete. Para cada paquete se prueban
   todas las posiciones posibles de su recogida y de su entrega (la recogida siempre antes) y se
   escoge la que menos alarga la ruta.
2. **Mejora local.** Sobre esa ruta se repiten dos movimientos mientras sigan acortándola:
   - *reubicar un paquete*: sacar su recogida y su entrega y volver a meterlas en el mejor lugar;
   - *mover un bloque*: tomar 1, 2 o 3 paradas seguidas y ponerlas en otra posición, solo si
     ningún paquete queda entregado antes de recogerse.
3. **Sacudir y volver a mejorar (búsqueda local iterada).** Cuando la mejora local ya no encuentra
   nada, se sacan de 2 a 6 paquetes (a veces al azar y a veces paquetes **cercanos entre sí**, para
   reorganizar un barrio completo), se vuelven a insertar y se repite la mejora local. La nueva ruta
   se acepta si no es más de un 2 % peor que la mejor encontrada; eso permite salir de "callejones"
   donde ningún cambio pequeño ayuda.
4. **Reinicios.** Todo lo anterior se repite 60 veces, empezando con un orden de paquetes distinto
   cada vez, y se queda la mejor ruta. El generador aleatorio usa una **semilla fija** (2026), así
   que el programa siempre imprime la misma ruta.

### Verificación

Antes de imprimir se llama a `IsValid(route)`, que comprueba que cada paquete se recoge
exactamente una vez y se entrega exactamente una vez después de recogerlo, en los puntos correctos.
El total impreso es la suma de los tramos impresos.

### Resultados

| Estrategia | Recorrido total |
|---|---:|
| Vecino más cercano (sección 8.3), sin mejoras | 40.52 km |
| Referencia del docente: inserción + reubicación + reinicios | 30.38 km |
| **Este programa** | **30.15 km** |

El vecino más cercano da exactamente los mismos 40.52 km de la referencia, lo que confirma que la
tabla de distancias (Dijkstra) es correcta. La ruta final tarda unos 2 segundos. En pruebas con
300 reinicios (más de un minuto de búsqueda) no apareció ninguna ruta por debajo de 30.15 km.

## Salida con `city.txt`, `packages.txt` y posición inicial `D`

```
Posición inicial: D
Paquetes: 30   Ubicaciones: 100   Calles: 142 (30 de un solo sentido)

 1. Ir de D a BE (620 m): recoger paquete 7.
      Camino: D -> BT -> BE
 2. Ir de BE a X (490 m): recoger paquete 12.
      Camino: BE -> E -> X
 3. Ir de X a CN (990 m): recoger paquetes 1, 2 y 3.
      Camino: X -> B -> BU -> CN
 4. Ir de CN a BU (430 m): recoger paquetes 8 y 28.
      Camino: CN -> BU
 5. Ir de BU a AM (350 m): recoger paquete 30.
      Camino: BU -> AM
 6. Ir de AM a CH (1150 m): recoger paquete 10.
      Camino: AM -> AK -> AX -> AQ -> BS -> CT -> CJ -> CH
 7. Ir de CH a BS (460 m): recoger paquete 25.
      Camino: CH -> CJ -> CT -> BS
 8. Ir de BS a CS (310 m): recoger paquete 19.
      Camino: BS -> CS
 9. Ir de CS a AP (770 m): recoger paquete 5.
      Camino: CS -> AE -> AP
10. Ir de AP a AT (350 m): recoger paquete 27.
      Camino: AP -> AT
11. Ir de AT a AQ (200 m): recoger paquete 9.
      Camino: AT -> AQ
12. Ir de AQ a AB (1270 m): recoger paquete 4.
      Camino: AQ -> AX -> F -> BV -> AB
13. Ir de AB a BV (470 m): entregar paquete 19.
      Camino: AB -> BV
14. Ir de BV a AX (630 m): recoger paquete 17.
      Camino: BV -> F -> AX
15. Ir de AX a BW (1260 m): entregar paquete 30.
      Camino: AX -> S -> K -> N -> BW
16. Ir de BW a R (630 m): recoger paquetes 6 y 21.
      Camino: BW -> CL -> R
17. Ir de R a CL (360 m): recoger paquete 14.
      Camino: R -> CL
18. Ir de CL a BN (450 m): entregar paquete 7.
      Camino: CL -> BN
19. Ir de BN a AO (1300 m): entregar paquete 3 y recoger paquete 11.
      Camino: BN -> Y -> AV -> CG -> AI -> AO
20. Ir de AO a AI (170 m): recoger paquete 24.
      Camino: AO -> AI
21. Ir de AI a BM (550 m): entregar paquetes 2 y 10.
      Camino: AI -> AS -> BM
22. Ir de BM a BI (1750 m): recoger paquete 13.
      Camino: BM -> AS -> BH -> BL -> CO -> BI
23. Ir de BI a Q (1380 m): recoger paquetes 22 y 29.
      Camino: BI -> CO -> AZ -> V -> O -> BY -> Q
24. Ir de Q a AA (1450 m): entregar paquete 12.
      Camino: Q -> BJ -> CI -> Z -> AA
25. Ir de AA a AJ (2230 m): entregar paquete 29 y recoger paquetes 23 y 26.
      Camino: AA -> Z -> CA -> BT -> P -> CB -> AD -> AR -> BB -> AJ
26. Ir de AJ a BB (400 m): recoger paquete 16.
      Camino: AJ -> BB
27. Ir de BB a AR (150 m): entregar paquete 8 y recoger paquete 20.
      Camino: BB -> AR
28. Ir de AR a AD (280 m): entregar paquetes 5 y 13.
      Camino: AR -> AD
29. Ir de AD a CP (670 m): entregar paquete 9.
      Camino: AD -> CB -> P -> BT -> CP
30. Ir de CP a D (460 m): recoger paquete 18.
      Camino: CP -> BT -> D
31. Ir de D a AC (370 m): entregar paquete 17.
      Camino: D -> AC
32. Ir de AC a CD (130 m): entregar paquete 25.
      Camino: AC -> CD
33. Ir de CD a BY (810 m): entregar paquete 16.
      Camino: CD -> BA -> BG -> BY
34. Ir de BY a BF (1170 m): entregar paquetes 18 y 23.
      Camino: BY -> O -> I -> BG -> BA -> AG -> BF
35. Ir de BF a BE (710 m): entregar paquete 21.
      Camino: BF -> X -> E -> BE
36. Ir de BE a T (440 m): entregar paquetes 24 y 27.
      Camino: BE -> T
37. Ir de T a CU (1360 m): recoger paquete 15.
      Camino: T -> CF -> W -> CV -> BX -> CU
38. Ir de CU a CV (360 m): entregar paquete 28.
      Camino: CU -> CV
39. Ir de CV a M (700 m): entregar paquete 1.
      Camino: CV -> W -> CM -> M
40. Ir de M a AE (430 m): entregar paquete 26.
      Camino: M -> CM -> AE
41. Ir de AE a AP (370 m): entregar paquetes 11 y 22.
      Camino: AE -> AP
42. Ir de AP a AT (350 m): entregar paquetes 14 y 15.
      Camino: AP -> AT
43. Ir de AT a AQ (200 m): entregar paquete 6.
      Camino: AT -> AQ
44. Ir de AQ a CT (350 m): entregar paquete 4.
      Camino: AQ -> BS -> CT
45. Ir de CT a CR (420 m): entregar paquete 20.
      Camino: CT -> CR

Todos los paquetes fueron recogidos y entregados.
Recorrido total: 30.15 km
```

## Salida del ejemplo pequeño (sección 6)

```
Posición inicial: A
Paquetes: 3   Ubicaciones: 7   Calles: 7 (1 de un solo sentido)

 1. Ir de A a C (400 m): recoger paquete 1.
      Camino: A -> B -> C
 2. Ir de C a G (620 m): entregar paquete 1 y recoger paquete 3.
      Camino: C -> B -> E -> F -> G
 3. Ir de G a E (270 m): recoger paquete 2.
      Camino: G -> F -> E
 4. Ir de E a A (550 m): entregar paquete 3.
      Camino: E -> B -> A
 5. Ir de A a D (600 m): entregar paquete 2.
      Camino: A -> B -> C -> D

Todos los paquetes fueron recogidos y entregados.
Recorrido total: 2.44 km
```
