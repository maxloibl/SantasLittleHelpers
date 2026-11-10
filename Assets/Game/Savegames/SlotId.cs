using System;

namespace SantasLittleHelpers.Savegames
{
    public readonly struct SlotId : IEquatable<SlotId>
    {
        private readonly Guid value;

        public SlotId(Guid value)
        {
            this.value = value;
        }

        public Guid Value => value;

        public bool IsEmpty => value == Guid.Empty;

        public static SlotId Empty => default;

        public static SlotId New()
        {
            return new SlotId(Guid.NewGuid());
        }

        public static bool TryParse(string text, out SlotId slotId)
        {
            if (!string.IsNullOrWhiteSpace(text) && Guid.TryParse(text, out Guid parsed) && parsed != Guid.Empty)
            {
                slotId = new SlotId(parsed);
                return true;
            }

            slotId = Empty;
            return false;
        }

        public static SlotId Parse(string text)
        {
            if (TryParse(text, out SlotId slotId))
            {
                return slotId;
            }

            throw new FormatException("A slot id must be a non-empty UUID.");
        }

        public string ToFileToken()
        {
            return value.ToString("N");
        }

        public bool Equals(SlotId other)
        {
            return value.Equals(other.value);
        }

        public override bool Equals(object obj)
        {
            return obj is SlotId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override string ToString()
        {
            return value.ToString("D");
        }

        public static bool operator ==(SlotId left, SlotId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SlotId left, SlotId right)
        {
            return !left.Equals(right);
        }

        public static implicit operator Guid(SlotId slotId)
        {
            return slotId.value;
        }

        public static implicit operator SlotId(Guid value)
        {
            return new SlotId(value);
        }
    }
}
