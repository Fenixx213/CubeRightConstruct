using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CubeRightConstruct
{
    public partial class MainWindow : Window
    {
        private Random _random = new Random();
        private int _cubeCount;
        private List<Point3D> _cubePositions = new List<Point3D>();
        private ModelVisual3D _cubeVisual;
        private ModelVisual3D _boundingCubeVisual;
        private List<Button> _optionButtons = new List<Button>();
        private HashSet<Point> _correctPattern;
        private string _correctView;
        private List<string> _views = new List<string> { "Вид сверху", "Вид слева", "Вид спереди" };
        private bool _isProcessing = false;
        private Point _lastMousePosition;
        private double theta = Math.PI / 2; // Azimuth (horizontal angle)
        private double phi = Math.PI / 3;   // Elevation (vertical angle)
        private readonly double radius = 10; // Distance from camera to origin
        private readonly double rotationSpeed = 0.005; // Adjusted for smoother camera rotation
        private DispatcherTimer statusTimer;

        public MainWindow()
        {
            InitializeComponent();

            statusTimer = new DispatcherTimer();
            statusTimer.Interval = TimeSpan.FromSeconds(2);
            statusTimer.Tick += (s, e) =>
            {
                StatusText.Text = "";
                statusTimer.Stop();
            };

            // Initialize camera
            var camera = new PerspectiveCamera
            {
                Position = new Point3D(0, 0, radius),
                LookDirection = new Vector3D(0, 0, -radius),
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 60
            };
            viewport.Camera = camera;
            UpdateCameraPosition();

            // Setup lighting
            SetupLighting(viewport);

            // Generate cubes and bounding cube
            GenerateCubes();
            ChooseRandomView();
            DrawViewOptions();
            _boundingCubeVisual = CreateBoundingCube();
            viewport.Children.Add(_boundingCubeVisual);
        }

        private void UpdateCameraPosition()
        {
            // Clamp phi to avoid flipping at the poles
            phi = Math.Max(0.1, Math.Min(Math.PI - 0.1, phi));

            // Convert spherical coordinates to Cartesian coordinates
            double x = -radius * Math.Sin(phi) * Math.Cos(theta);
            double y = radius * Math.Cos(phi);
            double z = radius * Math.Sin(phi) * Math.Sin(theta);

            var camera = (PerspectiveCamera)viewport.Camera;
            camera.Position = new Point3D(x, y, z);
            camera.LookDirection = new Vector3D(-x, -y, -z);
        }

        private void SetupLighting(Viewport3D viewport)
        {
            Model3DGroup modelGroup = new Model3DGroup();
            modelGroup.Children.Add(new AmbientLight(Color.FromRgb(255, 255, 255)));
            ModelVisual3D lighting = new ModelVisual3D();
            lighting.Content = modelGroup;
            viewport.Children.Add(lighting);
        }

        private ModelVisual3D CreateBoundingCube()
        {
            MeshGeometry3D mesh = new MeshGeometry3D();
            double size = 50.0; // Large size for bounding cube

            // Define the 8 vertices of the bounding cube
            Point3DCollection positions = new Point3DCollection
            {
                new Point3D(-size, -size, -size),
                new Point3D(size, -size, -size),
                new Point3D(size, size, -size),
                new Point3D(-size, size, -size),
                new Point3D(-size, -size, size),
                new Point3D(size, -size, size),
                new Point3D(size, size, size),
                new Point3D(-size, size, size)
            };

            // Define the triangles (two per face, 6 faces)
            Int32Collection indices = new Int32Collection
            {
                // Front
                0, 1, 2, 0, 2, 3,
                // Back
                5, 4, 7, 5, 7, 6,
                // Left
                4, 0, 3, 4, 3, 7,
                // Right
                1, 5, 6, 1, 6, 2,
                // Top
                3, 2, 6, 3, 6, 7,
                // Bottom
                4, 5, 1, 4, 1, 0
            };

            mesh.Positions = positions;
            mesh.TriangleIndices = indices;

            // Create a semi-transparent material
            DiffuseMaterial material = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)));

            GeometryModel3D cubeModel = new GeometryModel3D
            {
                Geometry = mesh,
                Material = material,
                BackMaterial = material
            };

            ModelVisual3D modelVisual = new ModelVisual3D
            {
                Content = cubeModel
            };

            // Attach mouse events for camera rotation
            viewport.MouseLeftButtonDown += (s, e) =>
            {
                if (!_isProcessing)
                {
                    _lastMousePosition = e.GetPosition(viewport);
                    e.Handled = true;
                }
            };

            viewport.MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && !_isProcessing)
                {
                    Point currentPosition = e.GetPosition(viewport);
                    double deltaX = currentPosition.X - _lastMousePosition.X;
                    double deltaY = currentPosition.Y - _lastMousePosition.Y;

                    // Update angles for horizontal (theta) and vertical (phi) rotation
                    theta -= deltaX * rotationSpeed;
                    phi -= deltaY * rotationSpeed;

                    UpdateCameraPosition();
                    _lastMousePosition = currentPosition;
                    e.Handled = true;
                }
            };

            return modelVisual;
        }

        private void GenerateCubes()
        {
            _cubeCount = _random.Next(7, 11);
            _cubePositions.Clear();

            cubeGroup.Children.Clear();

            var occupiedPositions = new List<Point3D> { new Point3D(0, 0, 0) };
            _cubePositions.Add(new Point3D(0, 0, 0));

            cubeGroup.Children.Add(CreateCube(0, 0, 0));
            var wireframe0 = CreateCubeWireframe();
            wireframe0.Transform = new TranslateTransform3D(0.5, 0.5, 0.5);
            cubeGroup.Children.Add(wireframe0);

            var possiblePositions = new List<Point3D>();
            AddPossiblePositions(new Point3D(0, 0, 0), possiblePositions, occupiedPositions);

            for (int i = 1; i < _cubeCount; i++)
            {
                if (possiblePositions.Count == 0) break;
                int index = _random.Next(possiblePositions.Count);
                var newPosition = possiblePositions[index];
                possiblePositions.RemoveAt(index);

                cubeGroup.Children.Add(CreateCube(newPosition.X, newPosition.Y, newPosition.Z));

                var wireframe = CreateCubeWireframe();
                wireframe.Transform = new TranslateTransform3D(newPosition.X + 0.5, newPosition.Y + 0.5, newPosition.Z + 0.5);
                cubeGroup.Children.Add(wireframe);

                occupiedPositions.Add(newPosition);
                _cubePositions.Add(newPosition);
                AddPossiblePositions(newPosition, possiblePositions, occupiedPositions);
            }

            UpdateCameraPosition();
        }

        private void AddPossiblePositions(Point3D position, List<Point3D> possiblePositions, List<Point3D> occupiedPositions)
        {
            double cubeSize = 1.0;
            var directions = new[]
            {
                new Point3D(position.X + cubeSize, position.Y, position.Z),
                new Point3D(position.X - cubeSize, position.Y, position.Z),
                new Point3D(position.X, position.Y + cubeSize, position.Z),
                new Point3D(position.X, position.Y - cubeSize, position.Z),
                new Point3D(position.X, position.Y, position.Z + cubeSize),
                new Point3D(position.X, position.Y, position.Z - cubeSize)
            };
            foreach (var pos in directions)
            {
                if (!occupiedPositions.Contains(pos) && !possiblePositions.Contains(pos))
                    possiblePositions.Add(pos);
            }
        }

        private GeometryModel3D CreateCube(double x, double y, double z)
        {
            var mesh = new MeshGeometry3D();
            double halfSize = 0.5;
            double centerX = x + halfSize;
            double centerY = y + halfSize;
            double centerZ = z + halfSize;

            var points = new Point3DCollection
            {
                new Point3D(centerX - halfSize, centerY - halfSize, centerZ - halfSize),
                new Point3D(centerX + halfSize, centerY - halfSize, centerZ - halfSize),
                new Point3D(centerX + halfSize, centerY + halfSize, centerZ - halfSize),
                new Point3D(centerX - halfSize, centerY + halfSize, centerZ - halfSize),
                new Point3D(centerX - halfSize, centerY - halfSize, centerZ + halfSize),
                new Point3D(centerX + halfSize, centerY - halfSize, centerZ + halfSize),
                new Point3D(centerX + halfSize, centerY + halfSize, centerZ + halfSize),
                new Point3D(centerX - halfSize, centerY + halfSize, centerZ + halfSize)
            };

            var indices = new Int32Collection
            {
                0,1,2, 0,2,3,
                4,6,5, 4,7,6,
                0,3,7, 0,7,4,
                1,5,6, 1,6,2,
                3,2,6, 3,6,7,
                0,4,5, 0,5,1
            };

            mesh.Positions = points;
            mesh.TriangleIndices = indices;

            var color = Color.FromRgb(65, 105, 225);
            return new GeometryModel3D
            {
                Geometry = mesh,
                Material = new DiffuseMaterial(new SolidColorBrush(color)),
                BackMaterial = new DiffuseMaterial(new SolidColorBrush(color))
            };
        }

        private GeometryModel3D CreateCubeWireframe()
        {
            MeshGeometry3D wireframeMesh = new MeshGeometry3D();
            double size = 0.5;
            double thickness = 0.015;

            Point3D[] vertices = new Point3D[]
            {
                new Point3D(-size, -size, -size),
                new Point3D(size, -size, -size),
                new Point3D(size, size, -size),
                new Point3D(-size, size, -size),
                new Point3D(-size, -size, size),
                new Point3D(size, -size, size),
                new Point3D(size, size, size),
                new Point3D(-size, size, size)
            };

            int[][] edges = new int[][]
            {
                new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 0 },
                new[] { 4, 5 }, new[] { 5, 6 }, new[] { 6, 7 }, new[] { 7, 4 },
                new[] { 0, 4 }, new[] { 1, 5 }, new[] { 2, 6 }, new[] { 3, 7 }
            };

            Point3DCollection positions = new Point3DCollection();
            Int32Collection indices = new Int32Collection();

            for (int i = 0; i < edges.Length; i++)
            {
                Point3D start = vertices[edges[i][0]];
                Point3D end = vertices[edges[i][1]];
                Vector3D dir = end - start;
                double length = dir.Length;
                dir.Normalize();

                Vector3D up = Math.Abs(dir.Y) < 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(0, 0, 1);
                Vector3D right = Vector3D.CrossProduct(dir, up);
                right.Normalize();
                up = Vector3D.CrossProduct(right, dir);
                up.Normalize();

                Point3D[] crossSectionStart = new Point3D[4];
                Point3D[] crossSectionEnd = new Point3D[4];
                for (int j = 0; j < 4; j++)
                {
                    double angle = j * Math.PI / 2;
                    Vector3D offset = (Math.Cos(angle) * right + Math.Sin(angle) * up) * thickness;
                    crossSectionStart[j] = start + offset;
                    crossSectionEnd[j] = end + offset;
                }

                int baseIndex = positions.Count;
                foreach (var p in crossSectionStart) positions.Add(p);
                foreach (var p in crossSectionEnd) positions.Add(p);

                int[] sideIndices = new int[]
                {
                    0, 1, 5, 0, 5, 4,
                    1, 2, 6, 1, 6, 5,
                    2, 3, 7, 2, 7, 6,
                    3, 0, 4, 3, 4, 7
                };
                foreach (int idx in sideIndices)
                {
                    indices.Add(baseIndex + idx);
                }
            }

            wireframeMesh.Positions = positions;
            wireframeMesh.TriangleIndices = indices;

            GeometryModel3D wireframeModel = new GeometryModel3D
            {
                Geometry = wireframeMesh,
                Material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0, 0, 0))),
                BackMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0, 0, 0)))
            };

            return wireframeModel;
        }

        private void DrawViewOptions()
        {
            drawingCanvas.Children.Clear();
            _optionButtons.Clear();

            _correctPattern = GetViewPattern(_correctView);
            var options = GenerateOptions(_correctPattern);

            while (options.Count < 4)
            {
                HashSet<Point> incorrect = GenerateIncorrectOption(_correctPattern);
                if (!options.Any(option => option.SetEquals(incorrect)))
                {
                    options.Add(incorrect);
                }
            }

            options = options.OrderBy(x => _random.Next()).ToList();

            TextBlock viewLabel = new TextBlock
            {
                Text = $"Вид: {_correctView}",
                Foreground = Brushes.Black,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            drawingCanvas.Children.Add(viewLabel);
            Canvas.SetTop(viewLabel, 0);
            Canvas.SetLeft(viewLabel, drawingCanvas.Width / 2 - viewLabel.Text.Length * 4);

            for (int i = 0; i < 4; i++)
            {
                int localIndex = i;
                var button = new Button
                {
                    Content = CreateOptionCanvas(options[localIndex]),
                    Width = 100,
                    Height = 100,
                    Margin = new Thickness(2),
                    Tag = options[localIndex]
                };
                button.Click -= CheckAnswer;
                button.Click += CheckAnswer;
                _optionButtons.Add(button);
                drawingCanvas.Children.Add(button);
            }

            Canvas.SetLeft(_optionButtons[0], 0);
            Canvas.SetTop(_optionButtons[0], 30);
            Canvas.SetLeft(_optionButtons[1], 110);
            Canvas.SetTop(_optionButtons[1], 30);
            Canvas.SetLeft(_optionButtons[2], 0);
            Canvas.SetTop(_optionButtons[2], 140);
            Canvas.SetLeft(_optionButtons[3], 110);
            Canvas.SetTop(_optionButtons[3], 140);
        }

        private Canvas CreateOptionCanvas(HashSet<Point> pattern)
        {
            var canvas = new Canvas { Width = 100, Height = 100 };
            foreach (var point in pattern)
            {
                var rect = new Rectangle
                {
                    Width = 20,
                    Height = 20,
                    Fill = Brushes.Yellow,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(rect, point.X * 20);
                Canvas.SetTop(rect, point.Y * 20);
                canvas.Children.Add(rect);
            }
            return canvas;
        }

        private HashSet<Point> GetViewPattern(string view)
        {
            var pattern = new HashSet<Point>();
            double minX = _cubePositions.Min(p => p.X);
            double minY = _cubePositions.Min(p => p.Y);
            double minZ = _cubePositions.Min(p => p.Z);
            double maxX = _cubePositions.Max(p => p.X);

            if (view == "Вид сверху")
            {
                var grouped = _cubePositions.GroupBy(p => new { p.X, p.Z }).Select(g => g.OrderByDescending(p => p.Y).First());
                foreach (var pos in grouped)
                    pattern.Add(new Point((int)(pos.X - minX), (int)(pos.Z - minZ)));
            }
            else if (view == "Вид слева")
            {
                var grouped = _cubePositions.GroupBy(p => new { p.Y, p.Z }).Select(g => g.OrderByDescending(p => p.X).First());
                foreach (var pos in grouped)
                {
                    int z = (int)(pos.Z - minZ);
                    int y = (int)(pos.Y - minY);
                    pattern.Add(new Point(3 - z, 2 - y));
                }
            }
            else if (view == "Вид спереди")
            {
                var grouped = _cubePositions.GroupBy(p => new { p.X, p.Y }).Select(g => g.OrderByDescending(p => p.Z).First());
                double maxHeight = _cubePositions.Max(p => p.Y) - minY;
                foreach (var pos in grouped)
                    pattern.Add(new Point((int)(pos.X - minX), (int)(maxHeight - (pos.Y - minY))));
            }

            return pattern;
        }

        private List<HashSet<Point>> GenerateOptions(HashSet<Point> correct)
        {
            var options = new List<HashSet<Point>> { new HashSet<Point>(correct) };
            while (options.Count < 4)
            {
                var incorrect = new HashSet<Point>(correct);
                if (incorrect.Count > 0)
                {
                    var toRemove = correct.ElementAt(_random.Next(correct.Count));
                    incorrect.Remove(toRemove);
                }
                if (incorrect.Count > 0 && !options.Contains(incorrect))
                {
                    options.Add(incorrect);
                }
            }
            return options;
        }

        private HashSet<Point> GenerateIncorrectOption(HashSet<Point> correctPattern)
        {
            HashSet<Point> incorrectPattern = new HashSet<Point>(correctPattern);
            if (incorrectPattern.Count > 1)
            {
                incorrectPattern.Remove(incorrectPattern.First());
            }
            else
            {
                incorrectPattern.Add(new Point(_random.Next(3), _random.Next(3)));
            }
            return incorrectPattern;
        }

        private void ChooseRandomView()
        {
            int index = _random.Next(_views.Count);
            _correctView = _views[index];
        }

        private void CheckAnswer(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;
            _isProcessing = true;

            if (sender is Button button && button.Tag is HashSet<Point> selectedPattern)
            {
                bool isCorrect = _correctPattern.SetEquals(selectedPattern);
                if (isCorrect)
                {
                    ShowStatus("Верно");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Restart();
                        _isProcessing = false;
                    }));
                }
                else
                {
                    ShowStatus("Неверно");
                    _isProcessing = false;
                }
            }
            else
            {
                _isProcessing = false;
            }
        }

        private void ShowStatus(string message)
        {
            StatusText.Text = message;
            StatusText.Visibility = Visibility.Visible;
            statusTimer.Stop();
            statusTimer.Start();
        }

        private void ClearGrid_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;
            _isProcessing = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Restart();
                _isProcessing = false;
            }));
        }

        private double maxY(IEnumerable<Point3D> points)
        {
            double minY = points.Min(p => p.Y);
            return points.Max(p => p.Y - minY);
        }

        private void Restart()
        {
            if (_cubeVisual != null)
            {
                viewport.Children.Remove(_cubeVisual);
                _cubeVisual = null;
            }

            GenerateCubes();
            ChooseRandomView();
            DrawViewOptions();
            // Reset camera
            theta = Math.PI / 2;
            phi = Math.PI / 3;
            UpdateCameraPosition();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}