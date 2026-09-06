import Typography from '@mui/material/Typography'

function App() {
  return (
    <div className='full-screen-center'>
      <div style={{ textAlign: 'center' }}>
        <Typography variant='h1' gutterBottom>
          Stockma
        </Typography>
        <Typography variant='body1' color='text.secondary'>
          Gestión de inventarios con control de lotes y vencimientos
        </Typography>
      </div>
    </div>
  )
}

export default App
