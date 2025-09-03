using ADOTabular.Interfaces;
using System.Collections.Generic;

namespace ADOTabular
{
    public class ADOTabularCalendarCollection:IEnumerable<ADOTabularCalendar>
    {
        private SortedList<string, ADOTabularCalendar> _calendars;
        private IADOTabularConnection _connection;

        public ADOTabularCalendarCollection(IADOTabularConnection connection)
        {
            _connection = connection;
            _calendars = new SortedList<string, ADOTabularCalendar>();
            _connection.Visitor.Visit(this);
        }



        public void Add(ADOTabularCalendar calendar)
        {
            _calendars.Add(calendar.Name, calendar);
        }

        public IEnumerator<ADOTabularCalendar> GetEnumerator()
        {
            return _calendars.Values.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Clear()
        {
            _calendars.Clear();
        }   

        public bool Contains(string calendarName)
        {
            return _calendars.ContainsKey(calendarName);
        }   
    }
}
