using System.Collections.Generic;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Учёт занятости сокетов на корпусе. Вынесен в интерфейс, чтобы логика выбора
    /// модификаторов работала как в сцене, так и в headless-симуляции.
    /// </summary>
    public interface ISocketProvider
    {
        bool HasFree(SocketType type);
        void Mount(ModifierDefinition definition, int level);
        void Reset();
    }

    /// <summary>
    /// Реализация без сцены: считает занятые слоты, не создавая игровых объектов.
    /// </summary>
    public sealed class HeadlessSocketProvider : ISocketProvider
    {
        readonly Dictionary<SocketType, int> _capacity = new Dictionary<SocketType, int>();
        readonly Dictionary<SocketType, int> _used = new Dictionary<SocketType, int>();
        readonly HashSet<string> _mounted = new HashSet<string>();

        public HeadlessSocketProvider(CarDefinition car)
        {
            if (car == null || car.Sockets == null)
                return;

            for (int i = 0; i < car.Sockets.Length; i++)
            {
                SocketCapacity socket = car.Sockets[i];

                if (socket.Type == SocketType.None || socket.Count <= 0)
                    continue;

                _capacity.TryGetValue(socket.Type, out int existing);
                _capacity[socket.Type] = existing + socket.Count;
            }
        }

        public bool HasFree(SocketType type)
        {
            if (type == SocketType.None)
                return true;

            _capacity.TryGetValue(type, out int capacity);
            _used.TryGetValue(type, out int used);
            return used < capacity;
        }

        public void Mount(ModifierDefinition definition, int level)
        {
            if (definition == null || !definition.RequiresSocket)
                return;

            // Повышение уровня занимает уже выделенный слот.
            if (!_mounted.Add(definition.Id))
                return;

            _used.TryGetValue(definition.RequiredSocket, out int used);
            _used[definition.RequiredSocket] = used + 1;
        }

        public void Reset()
        {
            _used.Clear();
            _mounted.Clear();
        }
    }
}
